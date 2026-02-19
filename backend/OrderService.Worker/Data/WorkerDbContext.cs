using Microsoft.EntityFrameworkCore;
using OrderService.Worker.Models;

namespace OrderService.Worker.Data;

public class WorkerDbContext : DbContext
{
    public WorkerDbContext(DbContextOptions<WorkerDbContext> opts) : base(opts) { }
    public DbSet<Order> Orders => Set<Order>();
    public DbSet<OutboxMessage> OutboxMessages => Set<OutboxMessage>();
}
