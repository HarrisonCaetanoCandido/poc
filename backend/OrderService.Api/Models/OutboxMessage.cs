using System.ComponentModel.DataAnnotations;

namespace OrderService.Api.Models;

public class OutboxMessage
{
    [Key]
    public Guid Id { get; set; }
    public string CorrelationId { get; set; } = null!;
    public string EventType { get; set; } = null!;
    public string Payload { get; set; } = null!;
    public DateTime CreatedAt { get; set; }
    public bool Processed { get; set; }
    public DateTime? ProcessedAt { get; set; }
}
