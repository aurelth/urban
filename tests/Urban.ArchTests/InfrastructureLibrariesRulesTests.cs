using FluentAssertions;

namespace Urban.ArchTests;

public class InfrastructureLibrariesRulesTests
{
    /// <summary>Bibliotecas que só a Infrastructure pode usar: banco de dados, hash de senha (BCrypt), JWT, Redis e fila de tarefas (Hangfire).</summary>
    private static readonly string[] InfrastructureLibraries = ["Microsoft.EntityFrameworkCore", "Npgsql", "BCrypt", "Microsoft.IdentityModel", "StackExchange.Redis", "Hangfire"];

    public static TheoryData<string, string> DomainAndApplication
    {
        get
        {
            TheoryData<string, string> data = [];
            foreach (string module in Estrutura.Modulos)
            {
                data.Add(module, "Domain");
                data.Add(module, "Application");
            }

            return data;
        }
    }

    [Theory]
    [MemberData(nameof(DomainAndApplication))]
    public void Domain_and_Application_do_not_depend_on_infrastructure_libraries(string module, string layer)
    {
        IEnumerable<string> references = Estrutura.Referencias(Estrutura.Carregar(module, layer));

        references.Should().NotContain(
            r => InfrastructureLibraries.Any(library => r.StartsWith(library, StringComparison.Ordinal)),
            "banco, hash de senha, JWT, Redis e fila de tarefas ficam na Infrastructure, atrás de portas definidas na Application");
    }
}