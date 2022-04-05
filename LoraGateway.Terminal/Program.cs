﻿using LoraGateway.BackgroundServices;
using LoraGateway.Handlers;
using LoraGateway.Services;
using LoraGateway.Services.CommandLine;
using LoraGateway.Services.Firmware;
using LoraGateway.Services.Firmware.RandomLinearCoding;
using LoraGateway.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Filters;
using Shouldly;

namespace LoraGateway;

public static class LoraGateway
{
    public static string GetUniqueLogFile(string postFix = "")
    {
        return Path.Combine(Directory.GetCurrentDirectory(),
            $"../../../Logs/logs-{DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss")}{postFix}.txt");
    }

    public static async Task Main(string[] args)
    {
        Log.Logger = new LoggerConfiguration()
#if DEBUG
            .MinimumLevel.Debug()
#else
                .MinimumLevel.Information()
#endif
            .MinimumLevel.Override("Microsoft", LogEventLevel.Information)
            .Enrich.FromLogContext()
            .WriteTo.Console(
                outputTemplate:
                "[{Timestamp:HH:mm:ss} {Level:u3}] ({SourceContext}) {Message:lj}{NewLine}{Exception}",
                restrictedToMinimumLevel: LogEventLevel.Information
            )
            .WriteTo.Logger(lc => lc
                .WriteTo.File(GetUniqueLogFile(), LogEventLevel.Information))
            .WriteTo.Logger(lc => lc
                .Filter.ByIncludingOnly(Matching.FromSource<SerialProcessorService>())
                .WriteTo.File(GetUniqueLogFile("_serial"), LogEventLevel.Debug))
            .CreateLogger();

        var offset = (byte)0x32;
        var seed = new ulong[]
        {
            (ulong)0x01d353e5f3993bb0 + offset,
            0x7b9c0df6cb193b20 * (ulong)(offset + 1),
            (ulong)0x7b9c0df6cb193b20 - offset,
            0x7b9c0df6cb193b20 * (ulong)(offset - 1)
        };
        var rng = new XoshiroImpl2(seed);
        byte[] data = new byte[1];
        rng.NextBytes(data);
        Log.Information("{Byte}",data[0]);
        
        // var lcg = new LCG();
        // Log.Information("{Byte}",lcg.Next());
        // Log.Information("{Byte}",lcg.Next());
        // Log.Information("{Byte}",lcg.Next());
        // Log.Information("{Byte}",lcg.Next());
        // Log.Information("{Byte}",lcg.Next());
        // Log.Information("{Byte}",lcg.Next());
        // Log.Information("{Byte}",lcg.Next());
        // Log.Information("{Byte}",lcg.Next());

        await CreateHostBuilder(args).RunConsoleAsync();
    }

    public static IHostBuilder CreateHostBuilder(string[] args)
    {
        return Host.CreateDefaultBuilder(args)
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
                services.AddSingleton<ExperimentService>();
                services.AddSingleton<RlncFlashBlobService>();
                services.AddSingleton<FuotaSessionHostedService>();
                services.AddHostedService<FuotaSessionHostedService>(p => p.GetService<FuotaSessionHostedService>());
                services.AddHostedService<SerialHostedService>();
                services.AddTransient<SerialCommandHandler>();
                services.AddTransient<SelectDeviceCommandHandler>();
                services.AddTransient<ListDeviceCommandHandler>();
                services.AddSingleton<ConsoleProcessorService>();
                services.AddTransient<RlncDecodingFailureSelfTestService>();
                services.AddHostedService<ConsoleHostedService>();

                services.AddEventBus(builder =>
                {
                    builder.AddInMemoryEventBus(subscriber =>
                    {
                        subscriber.Subscribe<InitFuotaSession, FuotaEventHandler>();
                        subscriber.Subscribe<DecodingUpdateEvent, FuotaEventHandler>();
                        subscriber.Subscribe<DecodingResultEvent, FuotaEventHandler>();
                        subscriber.Subscribe<StopFuotaSession, FuotaEventHandler>();
                    });
                });
            });
    }
}