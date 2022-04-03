using LoraGateway.Utils;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.Utils;

public class StatisticsTests
{
    [Fact]
    public void MedianCalculation()
    {
        float[] a = {
            0.555555582f,
            0.571428597f,
            0.571428597f,
            0.600000024f,
            0.666666687f,
            0.666666687f,
            0.714285731f,
            0.75f,
            0.857142866f
        };
        var medianIndex = Statistics.MedianIndex(0, a.Length - 1);
        medianIndex.ShouldBe(4);

        var iqr = Statistics.Quartiles(a);
        iqr.Median.ShouldBe(0.6666667f);
        iqr.Q3.ShouldBe(0.7321428655f);
        iqr.Q1.ShouldBe(0.571428597f);
    }
}