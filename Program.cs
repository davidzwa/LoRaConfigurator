using LoraGateway.BackgroundServices;
using LoraGateway.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace LoraGateway;

public static class PortChat
{
    public static async Task Main(string[] args)
    {
        var store = new DeviceDataStore();
        await store.LoadStore();

        await Host.CreateDefaultBuilder(args)
            .ConfigureServices((hostContext, services) =>
            {
                services.AddSingleton<DeviceDataStore>();
                services.AddTransient<SerialProcessorService>();
                services.AddHostedService<ConsoleHostedService>();
                services.AddHostedService<SerialHostedService>();
            })
            .RunConsoleAsync();
    }
}