using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using OptionsPatternDemo;
using OptionsPatternDemo.Options;

var builder = Host.CreateApplicationBuilder();

builder.Services.AddSingleton<App>();
builder.Services
    .AddOptions<AnimalOptions>()
    .Bind(builder.Configuration.GetSection(nameof(AnimalOptions)))
    .ValidateDataAnnotations();

var host = builder.Build();

App app = host.Services.GetRequiredService<App>();

app.Run();