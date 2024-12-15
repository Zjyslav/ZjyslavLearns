using ProgressReportingDemo.Services;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;

namespace ProgressReportingDemo.Endpoints;

public static class WebSocketEndpoints
{
    public static void MapProgressWebSocketEndpoint(this IEndpointRouteBuilder endpointBuilder)
    {
        endpointBuilder.Map("/WebSocket/Progress",
            async (
                HttpContext context,
                LongRunningTaskProgressService progressService,
                ILoggerFactory loggerFactory,
                CancellationToken cancellationToken) =>
            {
                if (context.WebSockets.IsWebSocketRequest)
                {
                    var logger = loggerFactory.CreateLogger("WebSocket Endpoint");

                    using var webSocket = await context.WebSockets.AcceptWebSocketAsync();
                    logger.LogInformation("WebSocket connection established.");

                    var progress = new Progress<ProgressReport>();
                    EventHandler<ProgressReport> handleProgressChanged = async (object? sender, ProgressReport report) =>
                    {
                        var json = JsonSerializer.Serialize(report);
                        var bytes = Encoding.UTF8.GetBytes(json);
                        try
                        {
                            logger.LogInformation("Sending progress report.");
                            if (webSocket.State == WebSocketState.Open)
                            {
                                await webSocket.SendAsync(
                                    new ArraySegment<byte>(bytes),
                                    WebSocketMessageType.Text,
                                    endOfMessage: true,
                                    cancellationToken);
                            }
                        }
                        catch (OperationCanceledException)
                        {
                            logger.LogInformation("WebSocket send operation cancelled.");
                        }
                        catch (ObjectDisposedException)
                        {
                            logger.LogWarning("Cancellation Token disposed already.");
                        }
                    };
                    progress.ProgressChanged += handleProgressChanged;
                    await progressService.MonitorProgress(progress, context.RequestAborted);
                    progress.ProgressChanged -= handleProgressChanged;
                    logger.LogInformation("Closing WebSocket connection.");
                }
                else
                {
                    context.Response.StatusCode = 400;
                }
            });
    }
}
