using LoraGateway.BackgroundServices;
using LoraGateway.Services;
using LoraGateway.Services.CommandLine;
using LoraGateway.Services.Firmware.RandomLinearCoding;
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

        var field = new GField();
        // var fieldPoly = field.GetIrreduciblePolynomial();
        // Console.WriteLine($"Constant GF-256 {0}",
        //     IrreduciblePolynomial.CheckIrreducibility(fieldPoly, fieldPoly.Length));
        //
        // int[] testPoly = {4, 7, 21, 28};
        // Console.WriteLine("Test {0}", IrreduciblePolynomial.CheckIrreducibility(testPoly, testPoly.Length));

        int[] testPoly2 = {1, 3, 1};
        Console.WriteLine("Test2 {0}", IrreduciblePolynomial.CheckIrreducibility(testPoly2, testPoly2.Length));
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