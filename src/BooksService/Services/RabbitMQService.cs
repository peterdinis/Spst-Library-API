using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using System.Text;
using Newtonsoft.Json;
using BooksService.Messages;
using BooksService.Interfaces;

namespace BooksService.Services
{
    public class RabbitMQService : IRabbitMQService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;
        private readonly string _categoryRequestQueueName = "category_exists_requests";
        private readonly string _authorRequestQueueName = "author_exists_requests";
        private readonly string _responseQueueName;
        private readonly Dictionary<string, TaskCompletionSource<object>> _pendingRequests;
        private readonly ILogger<RabbitMQService> _logger;

        public RabbitMQService(ILogger<RabbitMQService> logger)
        {
            _logger = logger;
            _pendingRequests = new Dictionary<string, TaskCompletionSource<object>>();

            var factory = new ConnectionFactory()
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest",
                Port = 5672
            };

            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Declare request queues
            _channel.QueueDeclare(queue: _categoryRequestQueueName,
                                durable: false,
                                exclusive: false,
                                autoDelete: false,
                                arguments: null);

            _channel.QueueDeclare(queue: _authorRequestQueueName,
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

            _logger.LogInformation("RabbitMQ Service initialized");
        }

        public async Task<CategoryExistsResponse> GetCategoryAsync(int categoryId)
        {
            var request = new CategoryExistsRequest
            {
                CategoryId = categoryId
            };

            return await SendRequestAsync<CategoryExistsResponse>(request, _categoryRequestQueueName);
        }

        public async Task<AuthorExistsResponse> GetAuthorAsync(int authorId)
        {
            var request = new AuthorExistsRequest
            {
                AuthorId = authorId
            };

            return await SendRequestAsync<AuthorExistsResponse>(request, _authorRequestQueueName);
        }

        private async Task<T> SendRequestAsync<T>(object request, string queueName) where T : class
        {
            var requestId = GetRequestId(request);
            var tcs = new TaskCompletionSource<object>();
            _pendingRequests[requestId] = tcs;

            var message = JsonConvert.SerializeObject(request);
            var body = Encoding.UTF8.GetBytes(message);

            var properties = _channel.CreateBasicProperties();
            properties.ReplyTo = _responseQueueName;
            properties.CorrelationId = requestId;
            properties.Type = request.GetType().Name; // Store message type for deserialization

            _channel.BasicPublish(exchange: "",
                                routingKey: queueName,
                                basicProperties: properties,
                                body: body);

            _logger.LogInformation("Sent request to {QueueName} with ID: {RequestId}", queueName, requestId);

            // Timeout after 30 seconds
            var timeoutTask = Task.Delay(TimeSpan.FromSeconds(30));
            var completedTask = await Task.WhenAny(tcs.Task, timeoutTask);

            if (completedTask == timeoutTask)
            {
                _pendingRequests.Remove(requestId);
                throw new TimeoutException($"Service response timeout for {queueName}");
            }

            var result = await tcs.Task;
            return result as T ?? throw new InvalidCastException($"Unexpected response type for {queueName}");
        }

        private void OnResponseReceived(object sender, BasicDeliverEventArgs ea)
        {
            var body = ea.Body.ToArray();
            var message = Encoding.UTF8.GetString(body);
            
            try
            {
                object response = null;
                var messageType = ea.BasicProperties.Type;

                // Deserialize based on message type
                if (messageType == nameof(CategoryExistsResponse))
                {
                    response = JsonConvert.DeserializeObject<CategoryExistsResponse>(message);
                }
                else if (messageType == nameof(AuthorExistsResponse))
                {
                    response = JsonConvert.DeserializeObject<AuthorExistsResponse>(message);
                }

                if (response != null && _pendingRequests.TryGetValue(ea.BasicProperties.CorrelationId, out var tcs))
                {
                    _pendingRequests.Remove(ea.BasicProperties.CorrelationId);
                    tcs.SetResult(response);
                    _logger.LogInformation("Received response for Request ID: {RequestId}", ea.BasicProperties.CorrelationId);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing response for Request ID: {RequestId}", ea.BasicProperties.CorrelationId);
            }
        }

        private string GetRequestId(object request)
        {
            var property = request.GetType().GetProperty("RequestId");
            return property?.GetValue(request)?.ToString() ?? Guid.NewGuid().ToString();
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