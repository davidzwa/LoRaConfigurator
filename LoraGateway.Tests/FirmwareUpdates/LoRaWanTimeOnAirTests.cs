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
        toaCalculation.TimePacket.ShouldBe(0.010304d, 0.00001);
        toaCalculation.PayloadSymbNumber.ShouldBe(28);

        var toaCalculation2 = LoRaWanTimeOnAir.GetTimeOnAir(22);
        toaCalculation2.TimePacket.ShouldBe(0.014143999999999999, 0.00001);
        toaCalculation2.PayloadSymbNumber.ShouldBe(43);
    }
}