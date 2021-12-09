using LoraGateway.BackgroundServices;
using LoraGateway.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace LoraGateway;

public static class PortChat
{
    public static async Task Main(string[] args)
    {

        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Information()
#else
                .MinimumLevel.Information()
#endif
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] ({SourceContext:l}) {Message:lj}{NewLine}{Exception}"
            )
            .CreateLogger();
        
        await Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<DeviceDataStore>();
                services.AddSingleton<SerialProcessorService>();
                services.AddSingleton<SerialWatcher>();
                services.AddHostedService<SerialHostedService>();
                services.AddSingleton<ConsoleProcessorService>();
                services.AddHostedService<ConsoleHostedService>();
            })
            .RunConsoleAsync();
    }
}