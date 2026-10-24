using FluentAssertions;

namespace Urban.ArchTests;

public class PersistenceRulesTests
{
    private static readonly string[] PersistenceLibraries = ["Microsoft.EntityFrameworkCore", "Npgsql"];

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
    public void Domain_and_Application_do_not_depend_on_persistence_libraries(string module, string layer)
    {
        IEnumerable<string> references = Estrutura.Referencias(Estrutura.Carregar(module, layer));

        references.Should().NotContain(
            r => PersistenceLibraries.Any(library => r.StartsWith(library, StringComparison.Ordinal)),
            "a persistência fica na Infrastructure, atrás de portas definidas na Application");
    }
}
