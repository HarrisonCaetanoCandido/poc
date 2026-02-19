using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.EntityFrameworkCore;
using OrderService.Worker.Data;
using OrderService.Worker.Models;

namespace OrderService.Worker.Services;

public class Worker : BackgroundService
{
    private readonly IServiceProvider _provider;

    public Worker(IServiceProvider provider) => _provider = provider;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                using var scope = _provider.CreateScope();
                var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();

                var pending = await db.OutboxMessages.Where(m => !m.Processed).OrderBy(m=>m.CreatedAt).ToListAsync(stoppingToken);
                var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Worker>>();
                foreach (var msg in pending)
                {
                    try
                    {
                        logger.LogInformation("Worker picked OutboxMessage {OutboxId} correlation {CorrelationId}", msg.Id, msg.CorrelationId);

                        // idempotent consumer: validate correlation
                        if (!Guid.TryParse(msg.CorrelationId, out var orderId))
                        {
                            logger.LogWarning("Invalid CorrelationId on Outbox {OutboxId}", msg.Id);
                            msg.Processed = true;
                            msg.ProcessedAt = DateTime.UtcNow;
                            await db.SaveChangesAsync(stoppingToken);
                            continue;
                        }

                        var order = await db.Orders.FindAsync(new object[] { orderId }, stoppingToken);
                        if (order == null)
                        {
                            logger.LogWarning("Order {OrderId} not found for Outbox {OutboxId}", orderId, msg.Id);
                            msg.Processed = true;
                            msg.ProcessedAt = DateTime.UtcNow;
                            await db.SaveChangesAsync(stoppingToken);
                            continue;
                        }

                        if (order.Status != OrderStatus.Pending)
                        {
                            logger.LogInformation("Order {OrderId} has status {Status}, skipping Outbox {OutboxId}", order.Id, order.Status, msg.Id);
                            msg.Processed = true;
                            msg.ProcessedAt = DateTime.UtcNow;
                            await db.SaveChangesAsync(stoppingToken);
                            continue;
                        }

                        // process within a transaction to keep order+outbox consistent
                        await using (var tx = await db.Database.BeginTransactionAsync(stoppingToken))
                        {
                            order.Status = OrderStatus.Processing;
                            await db.SaveChangesAsync(stoppingToken);

                            logger.LogInformation("Order {OrderId} set to Processing", order.Id);

                            // simulate processing
                            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

                            order.Status = OrderStatus.Finalized;
                            msg.Processed = true;
                            msg.ProcessedAt = DateTime.UtcNow;
                            await db.SaveChangesAsync(stoppingToken);

                            await tx.CommitAsync(stoppingToken);

                            logger.LogInformation("Order {OrderId} set to Finalized and Outbox {OutboxId} marked processed", order.Id, msg.Id);
                        }
                    }
                    catch (OperationCanceledException) { throw; }
                    catch (Exception ex)
                    {
                        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<Worker>>();
                        logger.LogError(ex, "Error processing Outbox {OutboxId}", msg.Id);
                        // do not mark processed so it can be retried
                    }
                }
            }
            catch (OperationCanceledException) { break; }
            catch (Exception)
            {
                // swallow/logging can be added
            }

            await Task.Delay(2000, stoppingToken);
        }
    }
}
