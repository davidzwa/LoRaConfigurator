﻿using LoraGateway.BackgroundServices;
using LoraGateway.Handlers;
using LoraGateway.Services;
using LoraGateway.Services.CommandLine;
using LoraGateway.Services.Firmware;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;

namespace LoraGateway;

public static class LoraGateway
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
        
        var unity = new GFSymbol(1);
        foreach(var i in Enumerable.Range(1, 255))
        {
            var val = new GFSymbol((byte)i);
            Console.WriteLine($"1/VAL {unity/val}");
        }

        return;

        await CreateHostBuilder(args).RunConsoleAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args) =>
        Host.CreateDefaultBuilder(args)
            .UseSerilog()
            .ConfigureServices((_, services) =>
            {
                services.AddSingleton<DeviceDataStore>();
                services.AddSingleton<FuotaManagerService>();
                services.AddSingleton<SerialProcessorService>();
                services.AddSingleton<SelectedDeviceService>();
                services.AddSingleton<SerialWatcher>();
                services.AddSingleton<MeasurementsService>();
                services.AddSingleton<BlobFragmentationService>();
                services.AddSingleton<RlncEncodingService>();
                services.AddSingleton<FuotaSessionHostedService>();
                services.AddHostedService<FuotaSessionHostedService>(p => p.GetService<FuotaSessionHostedService>());
                services.AddHostedService<SerialHostedService>();
                services.AddTransient<SerialCommandHandler>();
                services.AddTransient<SelectDeviceCommandHandler>();
                services.AddTransient<ListDeviceCommandHandler>();
                services.AddSingleton<ConsoleProcessorService>();
                services.AddHostedService<ConsoleHostedService>();

                services.AddEventBus(builder =>
                {
                    builder.AddInMemoryEventBus(subscriber =>
                    {
                        subscriber.Subscribe<InitFuotaSession, FuotaEventHandler>();
                        subscriber.Subscribe<StopFuotaSession, FuotaEventHandler>();
                    });
                });
            });
}