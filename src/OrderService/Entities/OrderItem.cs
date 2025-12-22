using System.ComponentModel.DataAnnotations;

namespace OrderService.Entities;

public class OrderItem
{
    public int Id { get; set; }
    
    public int OrderId { get; set; }
    
    public Order Order { get; set; } = null!;
    
    [Required]
    public int BookId { get; set; }
}
