using OrderService.DTOs;
using OrderService.Entities;

namespace OrderService.Services;

public interface IOrderService
{
    Task<OrderDto> CreateOrderAsync(CreateOrderDto createOrderDto);
    Task<OrderDto?> GetOrderByIdAsync(int id);
    Task<IEnumerable<OrderDto>> GetAllOrdersAsync(int pageNumber, int pageSize, string? userId);
    Task<OrderDto?> ReturnOrderAsync(int id);
    Task<OrderDto?> ExtendOrderAsync(int id);
    Task<bool> DeleteOrderAsync(int id);
}
