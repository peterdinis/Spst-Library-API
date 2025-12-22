using Microsoft.EntityFrameworkCore;
using OrderService.Data;
using OrderService.DTOs;
using OrderService.Entities;
using System.Text.Json;

namespace OrderService.Services;

public class OrderService : IOrderService
{
    private readonly OrderDbContext _context;
    private readonly IRabbitMQService _rabbitMQService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<OrderService> _logger;

    public OrderService(
        OrderDbContext context,
        IRabbitMQService rabbitMQService,
        IHttpClientFactory httpClientFactory,
        ILogger<OrderService> logger)
    {
        _context = context;
        _rabbitMQService = rabbitMQService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OrderDto> CreateOrderAsync(CreateOrderDto createOrderDto)
    {
        // Validate books
        var client = _httpClientFactory.CreateClient("BooksService");
        var orderItems = new List<OrderItem>();

        foreach (var bookId in createOrderDto.BookIds)
        {
            try
            {
                var response = await client.GetAsync($"/api/books/{bookId}");
                if (!response.IsSuccessStatusCode)
                {
                    throw new Exception($"Book with ID {bookId} not found or service unavailable.");
                }

                var content = await response.Content.ReadAsStringAsync();
                var book = JsonSerializer.Deserialize<BookDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                if (book == null || !book.IsAvailable)
                {
                    throw new Exception($"Book with ID {bookId} is not available.");
                }

                orderItems.Add(new OrderItem { BookId = bookId });
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error validating book {BookId}", bookId);
                throw;
            }
        }

        var order = new Order
        {
            UserId = createOrderDto.UserId,
            OrderDate = DateTime.UtcNow,
            DueDate = DateTime.UtcNow.AddDays(30), // Default 30 days loan
            Status = OrderStatus.Active,
            OrderItems = orderItems
        };

        _context.Orders.Add(order);
        await _context.SaveChangesAsync();

        var orderDto = MapToDto(order);
        
        // Publish event
        _rabbitMQService.PublishOrderCreated(orderDto);

        return orderDto;
    }

    public async Task<OrderDto?> GetOrderByIdAsync(int id)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        return order == null ? null : MapToDto(order);
    }

    public async Task<IEnumerable<OrderDto>> GetAllOrdersAsync(int pageNumber, int pageSize, string? userId)
    {
        var query = _context.Orders
            .Include(o => o.OrderItems)
            .AsQueryable();

        if (!string.IsNullOrEmpty(userId))
        {
            query = query.Where(o => o.UserId == userId);
        }

        var orders = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        return orders.Select(MapToDto);
    }

    public async Task<OrderDto?> ReturnOrderAsync(int id)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return null;

        order.Status = OrderStatus.Returned;
        order.ReturnDate = DateTime.UtcNow;

        await _context.SaveChangesAsync();
        return MapToDto(order);
    }

    public async Task<OrderDto?> ExtendOrderAsync(int id)
    {
        var order = await _context.Orders
            .Include(o => o.OrderItems)
            .FirstOrDefaultAsync(o => o.Id == id);

        if (order == null) return null;

        if (order.Status != OrderStatus.Active)
        {
            throw new InvalidOperationException("Can only extend active orders.");
        }

        order.DueDate = order.DueDate.AddDays(14); // Extend by 2 weeks
        await _context.SaveChangesAsync();
        return MapToDto(order);
    }

    public async Task<bool> DeleteOrderAsync(int id)
    {
        var order = await _context.Orders.FindAsync(id);
        if (order == null) return false;

        _context.Orders.Remove(order);
        await _context.SaveChangesAsync();
        return true;
    }

    private static OrderDto MapToDto(Order order) => new(
        order.Id,
        order.UserId,
        order.OrderDate,
        order.ReturnDate,
        order.DueDate,
        order.Status.ToString(),
        order.OrderItems.Select(oi => oi.BookId).ToList()
    );
}
