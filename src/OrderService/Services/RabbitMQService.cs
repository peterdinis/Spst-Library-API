using System.Text;
using System.Text.Json;
using OrderService.DTOs;
using RabbitMQ.Client;

namespace OrderService.Services;

public class RabbitMQService : IRabbitMQService, IDisposable
{
    private readonly IConnection _connection;
    private readonly IModel _channel;
    private readonly string _exchangeName = "order_events";
    private readonly ILogger<RabbitMQService> _logger;

    public RabbitMQService(IConfiguration configuration, ILogger<RabbitMQService> logger)
    {
        _logger = logger;
        var factory = new ConnectionFactory()
        {
            HostName = configuration["RabbitMQ:Host"] ?? "localhost",
            Port = int.Parse(configuration["RabbitMQ:Port"] ?? "5672"),
            UserName = configuration["RabbitMQ:Username"] ?? "guest",
            Password = configuration["RabbitMQ:Password"] ?? "guest"
        };

        try
        {
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();
            _channel.ExchangeDeclare(exchange: _exchangeName, type: ExchangeType.Fanout);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Could not connect to RabbitMQ");
            // In a real app we might want to throw or handle this more gracefully
            // For now, we'll just log it and risk null reference if we try to use it
        }
    }

    public void PublishOrderCreated(OrderDto order)
    {
        if (_channel == null || _channel.IsClosed)
        {
            _logger.LogWarning("RabbitMQ channel is not open. Cannot publish message.");
            return;
        }

        var message = JsonSerializer.Serialize(order);
        var body = Encoding.UTF8.GetBytes(message);

        _channel.BasicPublish(exchange: _exchangeName,
                              routingKey: "",
                              basicProperties: null,
                              body: body);
        
        _logger.LogInformation("Published OrderCreated event for Order ID: {OrderId}", order.Id);
    }

    public void Dispose()
    {
        _channel?.Close();
        _connection?.Close();
        _channel?.Dispose();
        _connection?.Dispose();
    }
}
