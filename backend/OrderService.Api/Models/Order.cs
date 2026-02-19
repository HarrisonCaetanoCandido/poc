using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace OrderService.Api.Models;

public enum OrderStatus { Pending, Processing, Finalized }

public class Order
{
    [Key]
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Client { get; set; } = null!;
    public string Product { get; set; } = null!;
    [Column(TypeName = "decimal(18,2)")]
    public decimal Value { get; set; }
    public OrderStatus Status { get; set; }
    public DateTime CreatedAt { get; set; }
}
