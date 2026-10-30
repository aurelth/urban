using System.Reflection;
using FluentAssertions;

namespace Urban.ArchTests;

/// <summary>Os módulos conversam só por contratos do SharedKernel: o Condominium pede, o Identity cumpre, e nenhum conhece o outro.</summary>
public class ContratosEntreModulosTests
{
    private const string NamespaceDosContratos = "Urban.SharedKernel.Contracts";

    public static TheoryData<string> Contratos => new("IManagerEnrollment", "IManagerProfile", "IPasswordConfirmation");

    [Theory]
    [MemberData(nameof(Contratos))]
    public void O_contrato_e_uma_interface_publica_do_SharedKernel(string nome)
    {
        Type? contrato = typeof(Urban.SharedKernel.AssemblyReference).Assembly.GetType($"{NamespaceDosContratos}.{nome}");

        contrato.Should().NotBeNull();
        contrato!.IsInterface.Should().BeTrue();
        contrato.IsPublic.Should().BeTrue();
    }

    [Theory]
    [InlineData("IManagerProfile")]
    [InlineData("IPasswordConfirmation")]
    public void O_contrato_e_implementado_pelo_Identity_e_so_por_ele(string nome)
    {
        Type contrato = typeof(Urban.SharedKernel.AssemblyReference).Assembly.GetType($"{NamespaceDosContratos}.{nome}")!;

        foreach (string modulo in Estrutura.Modulos)
        {
            bool implementa = ImplementacoesEm(modulo, contrato).Any();

            implementa.Should().Be(modulo == "Identity", "só o Identity tem as contas e as senhas ({0})", modulo);
        }
    }

    [Theory]
    [InlineData("IManagerProfile")]
    [InlineData("IPasswordConfirmation")]
    public void O_Condominium_usa_o_contrato_sem_conhecer_o_Identity(string nome)
    {
        Type contrato = typeof(Urban.SharedKernel.AssemblyReference).Assembly.GetType($"{NamespaceDosContratos}.{nome}")!;
        Assembly application = Estrutura.Carregar("Condominium", "Application");

        bool usa = application.GetTypes().Any(t => t.GetConstructors().Any(c => c.GetParameters().Any(p => p.ParameterType == contrato)));

        usa.Should().BeTrue();
        Estrutura.Referencias(application).Should().NotContain(r => r.StartsWith("Urban.Identity.", StringComparison.Ordinal));
    }

    [Fact]
    public void Os_contratos_nao_expoem_tipos_de_nenhum_modulo()
    {
        Assembly sharedKernel = typeof(Urban.SharedKernel.AssemblyReference).Assembly;

        Estrutura.Referencias(sharedKernel).Should().NotContain(r => r.StartsWith("Urban.", StringComparison.Ordinal) && r != "Urban.SharedKernel");
    }

    private static IEnumerable<Type> ImplementacoesEm(string modulo, Type contrato) =>
        Estrutura.Camadas.SelectMany(camada => Estrutura.Carregar(modulo, camada).GetTypes())
            .Where(t => t is { IsClass: true, IsAbstract: false } && contrato.IsAssignableFrom(t));
}
