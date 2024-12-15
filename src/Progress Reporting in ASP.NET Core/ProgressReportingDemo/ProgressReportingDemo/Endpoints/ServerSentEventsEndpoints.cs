using ProgressReportingDemo.Services;
using System.Text;
using System.Text.Json;

namespace ProgressReportingDemo.Endpoints;

public static class ServerSentEventsEndpoints
{
    public static void MapProgressServerSentEventsEndpoint(this IEndpointRouteBuilder endpointBuilder)
    {
        endpointBuilder.MapGet("/ServerSentEvents/Progress",
            async (
                HttpContext context,
                LongRunningTaskProgressService progressService,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            {
                var logger = loggerFactory.CreateLogger("ServerSentEvents Endpoint");

                if (context.Request.Headers.TryGetValue("Accept", out var accept) && accept.Contains("text/event-stream"))
                {
                    context.Response.Headers.Append("Content-Type", "text/event-stream");

                    var progress = new Progress<ProgressReport>();
                    EventHandler<ProgressReport> handleProgressChanged = async (object? sender, ProgressReport report) =>
                    {
                        var json = JsonSerializer.Serialize(report);
                        var bytes = Encoding.UTF8.GetBytes($"data: {json}\n\n");
                        try
                        {
                            logger.LogInformation("Sending progress report.");
                            await context.Response.Body.WriteAsync(bytes, cancellationToken);
                            await context.Response.Body.FlushAsync(cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            logger.LogInformation("Server-Sent Events send operation cancelled.");
                        }
                        catch (ObjectDisposedException)
                        {
                            logger.LogWarning("Cancellation Token disposed already.");
                        }
                    };
                    progress.ProgressChanged += handleProgressChanged;
                    await progressService.MonitorProgress(progress, cancellationToken);
                    progress.ProgressChanged -= handleProgressChanged;
                    logger.LogInformation("Closing Server-Sent Events connection.");
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            });
    }
}
