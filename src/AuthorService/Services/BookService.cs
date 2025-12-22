using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using Newtonsoft.Json;
using AuthorService.Interfaces;
using AuthorService.Messages;

namespace AuthorService.Services
{
    public class BookService : IBookService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _requestQueueName = "books_by_author_requests";
        private readonly string _responseQueueName;
        private readonly Dictionary<string, TaskCompletionSource<GetBooksByAuthorResponse>> _pendingRequests;
        private readonly ILogger<BookService> _logger;

        public BookService(ILogger<BookService> logger)
        {
            _logger = logger;
            _pendingRequests = new Dictionary<string, TaskCompletionSource<GetBooksByAuthorResponse>>();

            var factory = new ConnectionFactory()
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest",
                Port = 5672
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare request queue
            _channel.QueueDeclare(queue: _requestQueueName,
                                durable: false,
                                exclusive: false,
                                autoDelete: false,
                                arguments: null);

            // Create temporary response queue
            _responseQueueName = _channel.QueueDeclare().QueueName;

            // Set up consumer for responses
            var consumer = new EventingBasicConsumer(_channel);
            consumer.Received += OnResponseReceived;
            _channel.BasicConsume(queue: _responseQueueName,
                                autoAck: true,
                                consumer: consumer);

            _logger.LogInformation("Book Service (RabbitMQ) initialized for AuthorService");
        }

        public async Task<List<AuthorBookDto>> GetBooksByAuthorAsync(int authorId)
        {
            var response = await SendBooksRequestAsync(authorId);
            return response?.Books ?? new List<AuthorBookDto>();
        }

        public async Task<int> GetBooksCountByAuthorAsync(int authorId)
        {
            var response = await SendBooksRequestAsync(authorId);
            return response?.Books?.Count ?? 0;
        }

        private async Task<GetBooksByAuthorResponse> SendBooksRequestAsync(int authorId)
        {
            var request = new GetBooksByAuthorRequest
            {
                AuthorId = authorId
            };

            var tcs = new TaskCompletionSource<GetBooksByAuthorResponse>();
            _pendingRequests[request.RequestId] = tcs;

            var message = JsonConvert.SerializeObject(request);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = _channel.CreateBasicProperties();
            properties.ReplyTo = _responseQueueName;
            properties.CorrelationId = request.RequestId;
            properties.Type = nameof(GetBooksByAuthorRequest);

            _channel.BasicPublish(exchange: "",
                                routingKey: _requestQueueName,
                                basicProperties: properties,
                                body: body);

            _logger.LogInformation("Sent books by author request for Author ID: {AuthorId}", authorId);

            // Timeout after 30 seconds
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingRequests.Remove(request.RequestId);
                throw new TimeoutException("Book service response timeout");
            }

            return await tcs.Task;
        }

        private void OnResponseReceived(object sender, BasicDeliverEventArgs ea)
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            
            try
            {
                var response = JsonConvert.DeserializeObject<GetBooksByAuthorResponse>(message);
                
                if (response != null && _pendingRequests.TryGetValue(response.RequestId, out var tcs))
                {
                    _pendingRequests.Remove(response.RequestId);
                    tcs.SetResult(response);
                    _logger.LogInformation("Received books by author response for Request ID: {RequestId}, Books Count: {BooksCount}", 
                        response.RequestId, response.Books.Count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing books by author response");
            }
        }

        public void Dispose()
        {
            _channel?.Close();
            _connection?.Close();
            _channel?.Dispose();
            _connection?.Dispose();
        }
    }
}