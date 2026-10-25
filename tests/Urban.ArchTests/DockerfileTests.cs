using System.Xml.Linq;
using FluentAssertions;

namespace Urban.ArchTests;

/// <summary>
/// Os testes e o build do PR não montam a imagem Docker; quem monta é o CD, depois do merge. Um projeto novo que a API usa
/// e que ficou fora do infra/Dockerfile.api só aparece lá, com o deploy quebrado. Este teste antecipa o erro.
/// </summary>
public class DockerfileTests
{
    private const string ApiProject = "src/Urban.Api/Urban.Api.csproj";

    private static readonly string Root = FindRoot();

    [Fact]
    public void The_guard_sees_the_projects_the_api_depends_on()
    {
        string[] projects = [.. ProjectsReachableFromTheApi()];

        projects.Should().Contain(ApiProject).And.Contain("src/Urban.SharedKernel/Urban.SharedKernel.csproj");
        projects.Length.Should().BeGreaterThan(5);
    }

    [Fact]
    public void Every_project_the_api_depends_on_is_copied_into_the_api_image()
    {
        string[] copied = [.. File.ReadAllLines(Path.Combine(Root, "infra", "Dockerfile.api"))
            .Select(line => line.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(parts => parts.Length >= 3 && parts[0] == "COPY")
            .Select(parts => parts[1])];

        foreach (string project in ProjectsReachableFromTheApi())
        {
            string folder = project[..(project.LastIndexOf('/') + 1)];

            copied.Should().Contain(project, "o restore da imagem só enxerga os .csproj copiados antes dele ({0})", project);
            copied.Should().Contain(
                source => source.EndsWith('/') && folder.StartsWith(source, StringComparison.Ordinal),
                "o código de {0} precisa ser copiado para o publish", folder);
        }
    }

    private static IEnumerable<string> ProjectsReachableFromTheApi()
    {
        Queue<string> pending = new([ApiProject]);
        HashSet<string> seen = [];

        while (pending.Count > 0)
        {
            string project = pending.Dequeue();
            if (!seen.Add(project))
            {
                continue;
            }

            string folder = Path.GetDirectoryName(project)!;
            foreach (string include in XDocument.Load(Path.Combine(Root, project)).Descendants("ProjectReference").Select(e => (string)e.Attribute("Include")!))
            {
                string full = Path.GetFullPath(Path.Combine(Root, folder, include.Replace('\\', '/')));
                pending.Enqueue(Path.GetRelativePath(Root, full).Replace('\\', '/'));
            }
        }

        return seen;
    }

    private static string FindRoot()
    {
        DirectoryInfo? directory = new(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Urban.slnx")))
        {
            directory = directory.Parent;
        }

        return directory?.FullName ?? throw new InvalidOperationException("Não achei a raiz do repositório (Urban.slnx).");
    }
}