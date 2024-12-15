using Microsoft.AspNetCore.Components.Server.Circuits;

namespace ProgressReportingDemo.Components;

public class CancellingCircuitHandler : CircuitHandler
{
    private readonly ILogger<CancellingCircuitHandler> _logger;
    private readonly CancellationTokenSource _cts;
    public CancellingCircuitHandler(ILogger<CancellingCircuitHandler> logger, CancellationTokenSource cts)
    {
        _logger = logger;
        _cts = cts;
    }

    public override Task OnCircuitClosedAsync(Circuit circuit, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Circuit closed. Requesting cancellation.");
        _cts.Cancel();
        return Task.CompletedTask;
    }
}
