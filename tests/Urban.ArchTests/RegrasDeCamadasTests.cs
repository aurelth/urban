using FluentAssertions;
using NetArchTest.Rules;

namespace Urban.ArchTests;

public class RegrasDeCamadasTests
{
    public static TheoryData<string> Modulos => new(Estrutura.Modulos);

    [Theory]
    [MemberData(nameof(Modulos))]
    public void Domain_nao_deve_depender_de_Application_nem_de_Infrastructure(string modulo)
    {
        var resultado = Types.InAssembly(Estrutura.Carregar(modulo, "Domain"))
            .ShouldNot()
            .HaveDependencyOnAny($"Urban.{modulo}.Application", $"Urban.{modulo}.Infrastructure")
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Modulos))]
    public void Application_nao_deve_depender_de_Infrastructure(string modulo)
    {
        var resultado = Types.InAssembly(Estrutura.Carregar(modulo, "Application"))
            .ShouldNot()
            .HaveDependencyOnAny($"Urban.{modulo}.Infrastructure")
            .GetResult();

        resultado.IsSuccessful.Should().BeTrue();
    }

    [Theory]
    [MemberData(nameof(Modulos))]
    public void Domain_nao_deve_usar_pacotes_externos(string modulo)
    {
        var referencias = Estrutura.Referencias(Estrutura.Carregar(modulo, "Domain"));

        referencias.Should().OnlyContain(
            r => r.StartsWith("System", StringComparison.Ordinal)
                || r == "netstandard"
                || r == "mscorlib"
                || r == "Urban.SharedKernel",
            "o Domain só pode depender da plataforma .NET e do SharedKernel");
    }

    [Theory]
    [MemberData(nameof(Modulos))]
    public void Modulo_nao_deve_referenciar_outros_modulos(string modulo)
    {
        var outros = Estrutura.Modulos.Where(m => m != modulo).ToArray();

        foreach (var camada in Estrutura.Camadas)
        {
            var referencias = Estrutura.Referencias(Estrutura.Carregar(modulo, camada));

            referencias.Should().NotContain(
                r => outros.Any(o => r.StartsWith($"Urban.{o}.", StringComparison.Ordinal)),
                "o módulo {0} (camada {1}) não pode depender de outro módulo", modulo, camada);
        }
    }

    [Fact]
    public void SharedKernel_nao_deve_depender_de_nenhum_modulo()
    {
        var referencias = Estrutura.Referencias(typeof(Urban.SharedKernel.AssemblyReference).Assembly);

        referencias.Should().NotContain(r => r.StartsWith("Urban.", StringComparison.Ordinal) && r != "Urban.SharedKernel");
    }

    [Fact]
    public void EmailDelivery_nao_deve_depender_de_nenhum_modulo()
    {
        var referencias = Estrutura.Referencias(Urban.EmailDelivery.AssemblyReference.Assembly);

        referencias.Should().NotContain(
            r => r.StartsWith("Urban.", StringComparison.Ordinal) && r != "Urban.SharedKernel",
            "o envio de e-mail é um adaptador compartilhado: conhece só o SharedKernel");
    }

    [Fact]
    public void PostalCodeLookup_nao_deve_depender_de_nenhum_modulo()
    {
        var referencias = Estrutura.Referencias(Urban.PostalCodeLookup.AssemblyReference.Assembly);

        referencias.Should().NotContain(
            r => r.StartsWith("Urban.", StringComparison.Ordinal) && r != "Urban.SharedKernel",
            "a consulta de CEP é um adaptador compartilhado: conhece só o SharedKernel");
    }

    [Theory]
    [MemberData(nameof(Modulos))]
    public void Modulo_nao_deve_referenciar_o_adaptador_de_consulta_de_cep(string modulo)
    {
        foreach (var camada in Estrutura.Camadas)
        {
            var referencias = Estrutura.Referencias(Estrutura.Carregar(modulo, camada));

            referencias.Should().NotContain(
                "Urban.PostalCodeLookup",
                "o módulo {0} (camada {1}) fala com a porta IPostalCodeLookup, nunca com a BrasilAPI ou o ViaCEP", modulo, camada);
        }
    }

    [Theory]
    [MemberData(nameof(Modulos))]
    public void Modulo_nao_deve_referenciar_o_provedor_de_e_mail(string modulo)
    {
        foreach (var camada in Estrutura.Camadas)
        {
            var referencias = Estrutura.Referencias(Estrutura.Carregar(modulo, camada));

            referencias.Should().NotContain(
                "Urban.EmailDelivery",
                "o módulo {0} (camada {1}) fala com a porta IEmailSender, nunca com o provedor", modulo, camada);
        }
    }
}
