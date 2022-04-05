using System.Linq;
using LoraGateway.Utils;
using Shouldly;
using Xunit;

namespace LoraGateway.Tests.Utils;

public class RandomVectorTests
{
    [Fact]
    public void TestSystemRandomBytes()
    {
        var output = RandomVector.GeneratePseudoRandomBytes(10);
        output.Distinct().Count().ShouldBe(output.Length);
    }
}