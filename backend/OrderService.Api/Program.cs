using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using System.Text.Json;
using System.Text.Json.Serialization;
using OrderService.Api.Data;
using OrderService.Api.Models;
using Azure.Messaging.ServiceBus;

var builder = WebApplication.CreateBuilder(args);

var services = builder.Services;
var configuration = builder.Configuration;
// Ensure enums are serialized as strings so frontend receives readable status values
services.ConfigureHttpJsonOptions(opts =>
{
    opts.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
});
// CORS - allow frontend dev server origin (fallback to allow any for quick dev)
var frontendOrigin = Environment.GetEnvironmentVariable("VITE_FRONTEND_ORIGIN") ?? configuration.GetValue<string>("VITE_FRONTEND_ORIGIN") ?? "http://localhost:5173";
services.AddCors(options =>
{
    options.AddDefaultPolicy(policy =>
    {
        policy.WithOrigins(frontendOrigin)
              .AllowAnyHeader()
              .AllowAnyMethod();
    });
});

// Db
var conn = Environment.GetEnvironmentVariable("POSTGRES_CONN") ?? configuration.GetValue<string>("POSTGRES_CONN") ?? "Host=localhost;Database=ordersdb;Username=postgres;Password=postgres";
services.AddDbContext<AppDbContext>(opt => opt.UseNpgsql(conn));

// HealthChecks (Postgres + Service Bus)
services.AddHealthChecks()
    .AddNpgSql(conn, name: "postgres")
    .AddCheck<OrderService.Api.Health.ServiceBusHealthCheck>("servicebus");

services.AddEndpointsApiExplorer();

var app = builder.Build();

app.UseCors();

// optionally register ServiceBusClient for runtime use (not required)
var sbConn = Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTIONSTRING") ?? configuration.GetValue<string>("SERVICE_BUS_CONNECTIONSTRING");
ServiceBusClient? sbClient = null;
var sbQueue = Environment.GetEnvironmentVariable("SERVICE_BUS_QUEUE") ?? configuration.GetValue<string>("SERVICE_BUS_QUEUE");
if (!string.IsNullOrWhiteSpace(sbConn) && !string.IsNullOrWhiteSpace(sbQueue))
{
    try { sbClient = new ServiceBusClient(sbConn); }
    catch { sbClient = null; }
}

// Ensure DB migrations are applied at startup
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    db.Database.Migrate();
}

app.MapGet("/health", async ctx => ctx.Response.WriteAsJsonAsync(new { status = "ok" }));
app.MapHealthChecks("/hc", new HealthCheckOptions { ResponseWriter = async (ctx, report) => {
    ctx.Response.ContentType = "application/json";
    var result = new {
        status = report.Status.ToString(),
        checks = report.Entries.Select(e => new { name = e.Key, status = e.Value.Status.ToString() })
    };
    await ctx.Response.WriteAsync(JsonSerializer.Serialize(result));
}});

// Endpoints
app.MapPost("/orders", async (AppDbContext db, OrderCreateDto dto) =>
{
    var order = new Order {
        Client = dto.Client,
        Product = dto.Product,
        Value = dto.Value,
        Status = OrderStatus.Pending,
        CreatedAt = DateTime.UtcNow
    };
    db.Orders.Add(order);
    // outbox message
    var outbox = new OutboxMessage
    {
        Id = Guid.NewGuid(),
        CorrelationId = order.Id.ToString(),
        EventType = "OrderCreated",
        Payload = JsonSerializer.Serialize(new { order.Id, order.Client, order.Product, order.Value }),
        CreatedAt = DateTime.UtcNow,
        Processed = false
    };
    db.OutboxMessages.Add(outbox);
    await db.SaveChangesAsync();
    // Try publish to Azure Service Bus if configured. Fail silently and rely on outbox otherwise.
    if (sbClient is not null)
    {
        try
        {
            var sender = sbClient.CreateSender(sbQueue!);
            var message = new ServiceBusMessage(outbox.Payload)
            {
                MessageId = outbox.Id.ToString(),
                CorrelationId = order.Id.ToString()
            };
            message.ApplicationProperties["EventType"] = "OrderCreated";
            await sender.SendMessageAsync(message);
        }
        catch
        {
            // ignore - outbox ensures eventual processing
        }
    }

    return Results.Created($"/orders/{order.Id}", order);
});

app.MapGet("/orders", async (AppDbContext db) => await db.Orders.OrderByDescending(o=>o.CreatedAt).ToListAsync());

app.MapGet("/orders/{id}", async (AppDbContext db, Guid id) =>
{
    var order = await db.Orders.FindAsync(id);
    return order is null ? Results.NotFound() : Results.Ok(order);
});

app.Run();

// DTOs
public record OrderCreateDto(string Client, string Product, decimal Value);
