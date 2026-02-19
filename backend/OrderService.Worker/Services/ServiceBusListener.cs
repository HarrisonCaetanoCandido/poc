using System.Text.Json;
using Azure.Messaging.ServiceBus;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.DependencyInjection;
using OrderService.Worker.Data;
using OrderService.Worker.Models;

namespace OrderService.Worker.Services;

public class ServiceBusListener : BackgroundService
{
    private readonly ServiceBusClient _client;
    private readonly string _queueName;
    private ServiceBusProcessor? _processor;
    private readonly IServiceProvider _provider;

    public ServiceBusListener(ServiceBusClient client, IConfiguration config, IServiceProvider provider)
    {
        _client = client;
        _queueName = config["SERVICE_BUS_QUEUE"] ?? config["SERVICE_BUS_QUEUE"] ?? Environment.GetEnvironmentVariable("SERVICE_BUS_QUEUE") ?? "orders-queue";
        _provider = provider;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _processor = _client.CreateProcessor(_queueName, new ServiceBusProcessorOptions { AutoCompleteMessages = false, MaxConcurrentCalls = 1 });
        _processor.ProcessMessageAsync += MessageHandler;
        _processor.ProcessErrorAsync += ErrorHandler;
        await _processor.StartProcessingAsync(stoppingToken);

        await Task.Delay(Timeout.Infinite, stoppingToken);
    }

    private async Task MessageHandler(ProcessMessageEventArgs args)
    {
        var body = args.Message.Body.ToString();
        var correlation = args.Message.CorrelationId;
        var eventType = args.Message.ApplicationProperties.ContainsKey("EventType") ? args.Message.ApplicationProperties["EventType"]?.ToString() : null;

        if (string.IsNullOrWhiteSpace(correlation) || eventType != "OrderCreated")
        {
            await args.CompleteMessageAsync(args.Message);
            return;
        }

        if (!Guid.TryParse(correlation, out var orderId))
        {
            await args.CompleteMessageAsync(args.Message);
            return;
        }

        using var scope = _provider.CreateScope();
        var logger = scope.ServiceProvider.GetRequiredService<Microsoft.Extensions.Logging.ILogger<ServiceBusListener>>();

        try
        {
            var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
            var order = await db.Orders.FindAsync(new object[] { orderId });
            if (order is null)
            {
                logger.LogWarning("Order {OrderId} not found for ServiceBus message", orderId);
                await args.CompleteMessageAsync(args.Message);
                return;
            }

            if (order.Status != OrderStatus.Pending)
            {
                logger.LogInformation("Order {OrderId} has status {Status}, skipping ServiceBus message", order.Id, order.Status);
                await args.CompleteMessageAsync(args.Message);
                return;
            }

            await using (var tx = await db.Database.BeginTransactionAsync())
            {
                order.Status = OrderStatus.Processing;
                await db.SaveChangesAsync();

                logger.LogInformation("Order {OrderId} set to Processing via ServiceBus", order.Id);

                await Task.Delay(TimeSpan.FromSeconds(5));

                order.Status = OrderStatus.Finalized;
                await db.SaveChangesAsync();

                await tx.CommitAsync();

                logger.LogInformation("Order {OrderId} set to Finalized via ServiceBus", order.Id);
            }

            await args.CompleteMessageAsync(args.Message);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error handling ServiceBus message for Order {OrderId}", orderId);
            // message will be abandoned / retried
        }
    }

    private Task ErrorHandler(ProcessErrorEventArgs args)
    {
        // logging can be added
        return Task.CompletedTask;
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_processor != null)
        {
            await _processor.StopProcessingAsync(cancellationToken);
            await _processor.DisposeAsync();
        }
        await base.StopAsync(cancellationToken);
    }
}
