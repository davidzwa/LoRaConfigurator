﻿using LoraGateway.Services.Firmware;
using LoraGateway.Services.Firmware.RandomLinearCoding;
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

        var field = new GField();

        // await Host.CreateDefaultBuilder(args)
        //     .UseSerilog()
        //     .ConfigureServices((hostContext, services) =>
        //     {
        //         services.AddSingleton<DeviceDataStore>();
        //         services.AddSingleton<SerialProcessorService>();
        //         services.AddSingleton<SelectedDeviceService>();
        //         services.AddSingleton<SerialWatcher>();
        //         services.AddSingleton<MeasurementsService>();
        //         services.AddHostedService<SerialHostedService>();
        //         services.AddTransient<SerialCommandHandler>();
        //         services.AddTransient<SelectDeviceCommandHandler>();
        //         services.AddTransient<ListDeviceCommandHandler>();
        //         services.AddSingleton<ConsoleProcessorService>();
        //         services.AddHostedService<ConsoleHostedService>();
        //     })
        //     .RunConsoleAsync();
    }
}