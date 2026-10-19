using FluentAssertions;
using Urban.SharedKernel;

namespace Urban.UnitTests;

public class SharedKernelSmokeTests
{
    [Fact]
    public void AssemblyReference_deve_apontar_para_o_assembly_do_SharedKernel()
    {
        AssemblyReference.Assembly.GetName().Name.Should().Be("Urban.SharedKernel");
    }
}
