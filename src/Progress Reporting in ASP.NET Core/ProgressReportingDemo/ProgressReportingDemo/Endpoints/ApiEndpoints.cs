using ProgressReportingDemo.Services;

namespace ProgressReportingDemo.Endpoints;

public static class ApiEndpoints
{
    public static void MapProgressApiEndpoints(this IEndpointRouteBuilder endpointBuilder)
    {
        endpointBuilder.MapGet("/Api/Progress",
            (
                LongRunningTaskProgressService progressService,
                ILoggerFactory loggerFactory) =>
            {
                var logger = loggerFactory.CreateLogger("API Endpoint");

                logger.LogInformation("Received API request for progress report.");
                var progress = progressService.GetProgress();
                return Results.Ok(progress);
            });
    }
}
