using FluentAssertions;

namespace Urban.ArchTests;

public class BackgroundJobsRulesTests
{
    public static TheoryData<string> Modules => new(Estrutura.Modulos);

    [Fact]
    public void BackgroundJobs_does_not_depend_on_any_other_project_of_the_solution()
    {
        IEnumerable<string> references = Estrutura.Referencias(Urban.BackgroundJobs.AssemblyReference.Assembly);

        references.Should().NotContain(
            r => r.StartsWith("Urban.", StringComparison.Ordinal),
            "o processador de tarefas é infraestrutura compartilhada: não conhece nenhum módulo");
    }

    [Theory]
    [MemberData(nameof(Modules))]
    public void Modules_do_not_reference_the_shared_background_jobs_project(string module)
    {
        foreach (string layer in Estrutura.Camadas)
        {
            IEnumerable<string> references = Estrutura.Referencias(Estrutura.Carregar(module, layer));

            references.Should().NotContain(
                "Urban.BackgroundJobs",
                "o módulo {0} (camada {1}) só enfileira pela porta da Application; quem liga o Hangfire é a API", module, layer);
        }
    }
}