using System;
using System.Linq;
using LoraGateway.Utils;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.Utils;

public class XoShiRoTests
{
    [Fact]
    public void TestRngDistribution()
    {
        var systemRng = new Random();
        var seed = new byte[4]; 
        systemRng.NextBytes(seed);
        var rng = new XoshiroStarStar(seed); // new byte [] {0x32, 0x33, 0x34, 0x35});
        var data = rng.NextBytes(30000);

        // Simple duplicates validation
        data.Distinct().Count().ShouldBeLessThan(data.Length);

        var position = Should.NotThrow(() => data.First(d => d == 0x00));
        position.ShouldBe((byte)0x00);
        
        int[] occurrences = new int[256];
        for (int i = 0; i < 256; i++)
        {
            var amount = Should.NotThrow(() => data.Count(d => d == (byte)i));
            occurrences[i] = amount;
        }

        for (int i = 0; i < 256; i++)
        {
            occurrences[i].ShouldBeGreaterThan(0);
        }
    }
}