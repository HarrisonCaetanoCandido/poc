using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.EntityFrameworkCore;
using OrderService.Worker.Data;
using OrderService.Worker.Services;

var host = Host.CreateDefaultBuilder(args)
    .ConfigureServices((ctx, services) =>
    {
        var conn = Environment.GetEnvironmentVariable("POSTGRES_CONN") ?? ctx.Configuration.GetValue<string>("POSTGRES_CONN");
        services.AddDbContext<WorkerDbContext>(opt => opt.UseNpgsql(conn));
        services.AddHostedService<Worker>();

        // register ServiceBusClient if connection string present
        var sbConn = Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTIONSTRING") ?? ctx.Configuration.GetValue<string>("SERVICE_BUS_CONNECTIONSTRING");
        if (!string.IsNullOrWhiteSpace(sbConn))
        {
            services.AddSingleton(new Azure.Messaging.ServiceBus.ServiceBusClient(sbConn));
            services.AddHostedService<OrderService.Worker.Services.ServiceBusListener>();
        }
    })
    .Build();

using (var scope = host.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<WorkerDbContext>();
    db.Database.Migrate();
}

await host.RunAsync();
