using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Azure.Messaging.ServiceBus;

namespace OrderService.Api.Health;

public class ServiceBusHealthCheck : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        var conn = Environment.GetEnvironmentVariable("SERVICE_BUS_CONNECTIONSTRING");
        if (string.IsNullOrWhiteSpace(conn))
        {
            return Task.FromResult(HealthCheckResult.Degraded("SERVICE_BUS_CONNECTIONSTRING not configured"));
        }

        try
        {
            using var client = new ServiceBusClient(conn);
            // we don't call the network here; creating the client validates the connection string format
            return Task.FromResult(HealthCheckResult.Healthy("Service Bus connection string present"));
        }
        catch (System.Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("Service Bus client creation failed: " + ex.Message));
        }
    }
}
