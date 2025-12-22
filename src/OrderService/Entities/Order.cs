using System.ComponentModel.DataAnnotations;

namespace OrderService.Entities;

public enum OrderStatus
{
    Active,
    Returned,
    Overdue
}

public class Order
{
    public int Id { get; set; }
    
    [Required]
    public string UserId { get; set; } = string.Empty;
    
    public DateTime OrderDate { get; set; }
    
    public DateTime? ReturnDate { get; set; }
    
    public DateTime DueDate { get; set; }
    
    public OrderStatus Status { get; set; }
    
    public List<OrderItem> OrderItems { get; set; } = new();
}
