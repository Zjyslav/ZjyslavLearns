using Microsoft.AspNetCore.Components.Server.Circuits;
using ProgressReportingDemo.Components;
using ProgressReportingDemo.Endpoints;
using ProgressReportingDemo.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddSingleton<LongRunningTaskProgressService>();

builder.Services.AddScoped<CircuitHandler, CancellingCircuitHandler>();
builder.Services.AddScoped<CancellationTokenSource>();

builder.Services.AddRazorComponents()
    .AddInteractiveServerComponents();
builder.Services.AddRazorPages();

var app = builder.Build();

app.UseWebSockets();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Error", createScopeForErrors: true);
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseRouting();

app.UseAntiforgery();

app.MapStaticAssets();
app.MapRazorComponents<App>()
    .AddInteractiveServerRenderMode();
app.MapRazorPages();

app.MapProgressWebSocketEndpoint();
app.MapProgressServerSentEventsEndpoint();
app.MapProgressApiEndpoints();

app.Run();