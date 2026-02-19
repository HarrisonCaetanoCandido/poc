using System.ComponentModel.DataAnnotations;

namespace OrderService.Worker.Models;

public enum OrderStatus { Pending, Processing, Finalized }

public class Order
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Client { get; set; } = null!;
    public string Product { get; set; } = null!;
    public decimal Value { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
