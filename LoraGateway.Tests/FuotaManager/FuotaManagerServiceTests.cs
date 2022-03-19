using System;
using System.ComponentModel.DataAnnotations;
using LoraGateway.Services;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FuotaManager;

public class FuotaManagerServiceTests
{
    readonly IServiceProvider _services = 
        LoraGateway.CreateHostBuilder(new string[] { }).Build().Services;
    
    [Fact]
    public void ResolveFuotaManager()
    {
        var fuotaManagerService = _services.GetRequiredService<FuotaManagerService>();
        fuotaManagerService.ShouldNotBeNull();
    }

    [Fact]
    public void ShouldThrowForEssentialsOnlyAtStart()
    {
        var fuotaManagerService = _services.GetRequiredService<FuotaManagerService>();
        fuotaManagerService.IsFuotaSessionDone().ShouldBeTrue();
        fuotaManagerService.IsFuotaSessionEnabled().ShouldBeFalse();
        Should.Throw<ValidationException>(() => fuotaManagerService.GetCurrentSession());
        Should.NotThrow(() => fuotaManagerService.LogSessionProgress());
        Should.Throw<ValidationException>(() => fuotaManagerService.FetchNextRlncPayloadWithGenerator());
    }
}