using LoraGateway.Services.Firmware.LoRaPhy;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.FirmwareUpdates;

public class LoRaWanTimeOnAirTests
{
    [Fact]
    public void TimeOnAirCalculations()
    {
        var toaCalculation = LoRaWanTimeOnAir.GetTimeOnAir(10);
        toaCalculation.TimePacket.ShouldBe(0.041216, 0.00001);
        toaCalculation.PayloadSymbNumber.ShouldBe(28);
        
        var toaCalculation2 = LoRaWanTimeOnAir.GetTimeOnAir(22);
        toaCalculation2.TimePacket.ShouldBe(0.05657600000000001, 0.00001);
        toaCalculation2.PayloadSymbNumber.ShouldBe(43);
    }
}