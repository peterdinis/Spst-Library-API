using OrderService.DTOs;

namespace OrderService.Services;

public interface IRabbitMQService
{
    void PublishOrderCreated(OrderDto order);
}
