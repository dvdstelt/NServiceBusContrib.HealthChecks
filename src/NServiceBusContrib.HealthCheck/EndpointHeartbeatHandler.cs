using Microsoft.Extensions.DependencyInjection;
using NServiceBus;
using NServiceBusContrib.WarmUp;

namespace NServiceBusContrib.HealthCheck;

/// <summary>
/// Handles the <see cref="EndpointHeartbeat"/> message. Successful processing refreshes the
/// endpoint's heartbeat, which is what proves the message pump is actually alive.
/// </summary>
[Handler]
public class EndpointHeartbeatHandler(IServiceProvider serviceProvider)
{
    /// <summary>Refreshes the endpoint's heartbeat in the status registry.</summary>
    public Task Handle(EndpointHeartbeat message, IMessageHandlerContext context)
    {
        serviceProvider.GetService<IEndpointStatusRegistry>()
            ?.ReportHeartbeat(message.EndpointName, TimeSpan.FromTicks(message.StaleAfterTicks));
        return Task.CompletedTask;
    }
}
