﻿using LoRa;
using LoraGateway.BackgroundServices;
using LoraGateway.Handlers;
using LoraGateway.Services;
using LoraGateway.Services.CommandLine;
using LoraGateway.Services.Firmware;
using LoraGateway.Services.Firmware.Utils;
using LoraGateway.Utils;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Serilog;
using Serilog.Events;
using Serilog.Filters;

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

        // byte[,] inputBytes = {
        //     { 0x04, 0x82, 0x41, 0xa0, 0xd0, 0x00, 0x00, 0x00, 0x9a, 0x78 },
        //     { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
        //     { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
        //     { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 },
        //     { 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00, 0x00 }
        // };
        // var resultingMatrix = inputBytes.BytesToMatrix();
        // var matrixString = SerialUtil.MatrixToString(resultingMatrix);
        // Log.Information("\n{MatrixRow}", matrixString);
        // var matrix = RlncDecodingService.DecodeMatrix(resultingMatrix, 5);
        // var gfString = SerialUtil.MatrixToString(matrix);
        // Log.Information("\n{MatrixRow}", gfString);

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
                services.AddSingleton<RlncFlashBlobService>();
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
                        subscriber.Subscribe<DecodingUpdateEvent, FuotaEventHandler>();
                        subscriber.Subscribe<StopFuotaSession, FuotaEventHandler>();
                    });
                });
            });
}