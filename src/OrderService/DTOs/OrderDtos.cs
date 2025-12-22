using OrderService.Entities;

namespace OrderService.DTOs;

public record OrderDto(
    int Id,
    string UserId,
    DateTime OrderDate,
    DateTime? ReturnDate,
    DateTime DueDate,
    string Status,
    List<int> BookIds
);

public record CreateOrderDto(
    string UserId,
    List<int> BookIds
);

public record UpdateOrderDto(
    OrderStatus Status
);
