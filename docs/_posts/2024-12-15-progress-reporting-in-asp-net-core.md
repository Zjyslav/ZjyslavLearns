---
layout: post
title: Progress Reporting in ASP.NET Core
description: How to display progress of a long-running task
github-link:
date: 2024-12-15 15:14 +0100
---

There are times when some task takes too much time to let the user wait until it's finished before sending a response. Whether it's processing a file or waiting for a few external services, sometimes it's better to start the task in the background and update the user on the progress.

Here are some ways you can do just that in ASP.NET Core.

# The Setup

For the example app I started with a **.NET 9 Blazor server app** template without sample pages and added **Razor Pages** to it.

The progress to report will be represented with a simple record:

```
public record ProgressReport(int Percentage, DateTimeOffset TimeStamp);
```

The long running task is simulated with `LongRunningTaskProgressService`. It has 2 methods:

- `GetProgress()` returns a progress report,
- `MonitorProgress()` uses `IProgress<ProgressReport>` interface to report progress each 0.5 seconds.

```
public partial class LongRunningTaskProgressService
{
    private int _percentage = 0;
    private readonly Random _random = new();
    private readonly ILogger<LongRunningTaskProgressService> _logger;
    private Lock _lock = new();
    private DateTimeOffset _nextUpdate = DateTimeOffset.Now;

    public LongRunningTaskProgressService(ILogger<LongRunningTaskProgressService> logger)
    {
        _logger = logger;
    }

    public ProgressReport GetProgress()
    {
        _logger.LogInformation("Getting progress");

        lock (_lock)
        {
            if (DateTimeOffset.Now > _nextUpdate)
            {
                _logger.LogInformation("Updating progress");
                _percentage += 1;

                // Reset the percentage to 0 when it reaches 100
                if (_percentage >= 100)
                {
                    _percentage = 0;
                }

                _nextUpdate = DateTimeOffset.Now.AddSeconds((double)_random.Next(1, 10) / 10);
            }
            return new ProgressReport(_percentage, DateTimeOffset.Now);
        }
    }

    public async Task MonitorProgress(IProgress<ProgressReport> progress, CancellationToken cancellationToken)
    {
        _logger.LogInformation("Monitoring progress");

        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(500);
            progress.Report(GetProgress());
        }

        _logger.LogInformation("Monitoring progress cancelled");
    }
}
```

As you can tell, it's not a realistic simulation. Progress percentage is increased only when somebody asks for it and it resets after reaching 100%. I wanted to keeps things relatively simple.

The service is registered as a singleton, so the progress reported is the same for each connection.

# The Solutions

Here are 4 ways I used to report progress to the user in real time.

## Blazor Server

I started with Blazor Server, because the real time updates are already built-in. It's the only solution without any additional JS (aside from the JS needed for Blazor to run in the first place).

I modified the `Home.razor` page to display the progress.

```
@page "/Blazor"
@using Microsoft.AspNetCore.Components.Server.Circuits
@using ProgressReportingDemo.Services
@inject LongRunningTaskProgressService ProgressService
@inject CancellationTokenSource CancellationTokenSource
@inject ILogger<Home> Logger

@{
    var pageTitle = "Progress Reporting Demo - Blazor Server";
}

<PageTitle>@pageTitle</PageTitle>

<h1>@pageTitle</h1>

@if (_progressReport is not null)
{
    <p>Progress: <strong>@(_progressReport.Percentage)%</strong></p>
    <progress value="@_progressReport.Percentage" max="100"></progress>
    <p>Last update: <strong>@_progressReport.TimeStamp.LocalDateTime</strong></p>
}
else
{
    <p>Waiting for progress...</p>
}

<a href="/">Back</a>

@code {
    private ProgressReport? _progressReport;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            Progress<ProgressReport> progress = new Progress<ProgressReport>(report =>
            {
                Logger.LogInformation("Progress report received.");
                _progressReport = report;
                StateHasChanged();
            });
            await ProgressService.MonitorProgress(progress, CancellationTokenSource.Token);
        }
    }
}
```

### Reporting progress

I inject the service and start monitoring progress after the page is rendered the first time.

If you've never used `Progress<T>` and `IProgress<T>`, here's a quick explanation of how they work.

`Progress<T>` implements `IProgress<T>` explicitly, which means that the `IProgress<T>.Report(T)` method is only available via the interface. Because of that, the same `Progress<T>` object can be used in 2 places differently:

- as `Progress<T>` where you want to receive progress reports of type `T`
- as `IProgress<T>` where you want to report the progress.

In my example, `MonitorProgress` method asks for `IProgress<ProgressReport> progress` and each time it wants to report progress, it calls `GetProgress()` to get a new `ProgressReport` and provides it as an argument for `progress.Report()` method.

This method in turn invokes the `ProgressChanged` event. Handler for this event is provided to the `Progress<ProgressReport>` constructor. When the report is received, I log that I received it, replace old `_progressReport` value and ask Blazor for a new render with `StateHasChanged()`.

### Cancelling monitoring the progress

Aside from the `IProgress<ProgressReport>`, `MonitorProgress` asks also for a `CancellationToken`. If you look back at what happens in this method, you'll see that the token is it's only hope of ever escaping the `while (!cancellationToken.IsCancellationRequested)` loop. Without properly cancelling this task when it's no longer needed, it would just keep going on the server long after the user closed the page.

It's easy to just create a `CancellationTokenSource cts`, provide the method with `cts.Token` and call `cts.Cancel()` when I'm done, but I need to do it in reaction to the user leaving the page.

In Blazor you can use `NavigationManager.LocationChanged` event to trigger some code when the user changes the page, but it won't work in my case - my other pages are Razor pages, not Blazor pages - `NavigationManager` works only within the context of Blazor. It also doesn't handle the case of just closing the browser tab.

User's active connection with Blazor Server is called a **Circuit** and I want to stop reporting progress when it's closed. I managed to do it by replacing the default `CircuitHandler` with my own `CancellingCircuitHandler`:

```
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
```

In `Program.cs`:

```
builder.Services.AddScoped<CircuitHandler, CancellingCircuitHandler>();
builder.Services.AddScoped<CancellationTokenSource>();
```

I inject the same `CancellationTokenSource` instance to both `CancellingCircuitHandler` and to `Home` page where I provide its token to the `MonitorProgress` method. When the circuit is closed, progress monitoring is cancelled.

## WebSocket

WebSocket is a 2-way communication protocol. It's widely used for various apps that need real-time interactions. Using it to just send progress updates might be an overkill, but if you needed to extend the functionality, e.g. to allow user to cancel the processing or modify some parameters on the fly, it could all be handled with a WebSocket.

To enable the use of WebSockets, all I needed to do was add this one line in `Program.cs`:

```
app.UseWebSockets();
```

First I tried to add a WebSocket endpoint as a Razor Page handler method, but because of something in the request pipeline, the requests would not be recognized as WebSocket requests, so I decided to use minimal API endpoints instead (and keep this approach for the remaining methods).  
I might come back to it in the future and debug my way to a working solution, but not today.

```
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
```

And in `Program.cs`

```
app.MapProgressWebSocketEndpoint();
```

Cancelling progress monitoring is easier than in case of Blazor, because you can request a `CancellationToken` from dependency injection and it gets cancelled when connection is closed. You just need to be a litte careful, because when the connection is closed, it gets disposed and throws an `ObjectDisposedException` if it happens before all the code using it ends execution, which happens sometimes.

Opening the connection and receiving data using JavaScript is as easy as:

```
const socket = new WebSocket("wss://" + window.location.host + "/WebSocket/Progress");

socket.onmessage = function (event) {
    console.log("WebSocket message received:", event.data);

    const progressReport = JSON.parse(event.data);

    percentageElement.textContent = progressReport.Percentage + "%";
    progressBarElement.value = progressReport.Percentage;
    lastUpdateElement.textContent = new Date(progressReport.TimeStamp).toLocaleString();
};
```

Please note that instead of `https://` URL starts with `wss://` (or `ws://` for `http://`).

## Server-Sent Events

A communication mechanism more suited for our use-case would be Server-Sent Events (SSE). It's basically an HTTP connection that is kept open to let the server continue sending data. It's up to the server to decide when to send data, so there's no need for the client to keep asking for updates (see **API Endpoint** section for an example of exactly that).

Since it's still HTTP, it's set almost just like a regular get endpoint. All that needs to be changed is one header in the response. However, I've come across some information that some web servers might need to have slight configuration changes to allow for the connections to be open for longer periods of time.

```
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

```

And in `Program.cs`

```
app.MapProgressServerSentEventsEndpoint();
```

The key points are:

- Set `"Content-Type"` header with value of `"text/event-stream"`
- Start sent data with `"data: "`

To receive the data I only need a few lines of JS:

```
const eventSource = new EventSource("/ServerSentEvents/Progress");

eventSource.onmessage = function (event) {
    console.log("Server-Sent Event received:", event.data);

    const progressReport = JSON.parse(event.data);

    percentageElement.textContent = progressReport.Percentage + "%";
    progressBarElement.value = progressReport.Percentage;
    lastUpdateElement.textContent = new Date(progressReport.TimeStamp).toLocaleString();
};
```

## API Endpoint

Probably the easiest method from the server perspective, but a little more complicated for the clients, because they needs to decide for themselves when to ask for an update.

There are many ways you could approach trying to make lighter for the server (e.g. caching, ETags, long polling), but these are outside of scope for this post.

Well, now that I think about it long polling might be a suitable method to include, but I'll leave it for another day.

What I'm presenging here is short polling - a less sophisticated method.

```
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
```

And in `program.cs`:

```
app.MapProgressApiEndpoints();
```

Since it's just standard API stuff, if I decided not to include logging, I could fit it in one (rather long) line.

```
public static void MapProgressApiEndpoints(this IEndpointRouteBuilder endpointBuilder) => endpointBuilder.MapGet("/Api/Progress", (LongRunningTaskProgressService progressService) => Results.Ok(progressService.GetProgress()));
```

Here's JavaScript I wrote to ask for an update every half a second:

```
const updateInterval = 500;

startPeriodicExecution(updateProgress, updateInterval);

function startPeriodicExecution(func, period)
{
    setTimeout(async () => {
        const continueExecution = await func();
        if (continueExecution) {
            setInterval(func, period);
        }
    }, period);
}

async function updateProgress() {
    const response = await fetch('/Api/Progress');

    if (!response.ok) {
        console.error('Failed to fetch progress:', response);
        return false;
    }

    const progressReport = await response.json();

    percentageElement.textContent = progressReport.percentage + "%";
    progressBarElement.value = progressReport.percentage;
    lastUpdateElement.textContent = new Date(progressReport.timeStamp).toLocaleString();

    return true;
}
```

For production purposes it would propbably need some error handling, but for demo purposes, I just wanted the necessary pieces.

# Summary

This list is probably far from exhaustive and probably quite obvious for any seasoned web-dev. I'm actually pleasantly surprised how straightforward to use these technologies like WebSockets and SSE are once I spend a little time learn them. I get a little overwhelmed when I look at everything I've yet to learn, but these few small steps feel encouraging.

And yes, I know that the devs before me fought and died, so that these can be so easy to use. Their sacrifice will never be forgotten.
