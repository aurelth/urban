using System.Text.RegularExpressions;
using FluentAssertions;

namespace Urban.ArchTests;

/// <summary>
/// O nginx do container web é o primeiro a ver o corpo da requisição. Sem <c>client_max_body_size</c> ele recusa com 413 tudo
/// o que passa de 1 MB, antes de a API ouvir falar do pedido: a resposta do administrador leva até 10 MB de anexos.
/// </summary>
public partial class NginxTemplateTests
{
    // A API aceita até 16 MiB no corpo da resposta (10 MiB de anexos mais o texto e o multipart); o nginx do servidor usa 20m.
    private const int RequiredMegabytes = 16;

    private static readonly string Template = File.ReadAllText(
        Path.Combine(RepositoryRoot.Path, "infra", "nginx", "default.conf.template"));

    [Fact]
    public void The_api_location_accepts_the_largest_body_the_api_accepts()
    {
        string block = LocationBlock("/api/");

        Match limit = BodyLimit().Match(block);

        limit.Success.Should().BeTrue("sem client_max_body_size o nginx recusa corpos acima de 1 MB com 413");
        int.Parse(limit.Groups["size"].Value).Should().BeGreaterThanOrEqualTo(RequiredMegabytes);
    }

    [Fact]
    public void The_limit_is_not_widened_to_the_whole_server()
    {
        string withoutLocations = LocationBlocks().Replace(Template, string.Empty);

        BodyLimit().IsMatch(withoutLocations).Should().BeFalse("só a rota da API recebe corpo grande; o resto do site não");
    }

    private static string LocationBlock(string path)
    {
        Match match = LocationBlocks().Matches(Template).Single(m => m.Groups["path"].Value == path);
        return match.Value;
    }

    [GeneratedRegex(@"client_max_body_size\s+(?<size>\d+)m\s*;")]
    private static partial Regex BodyLimit();

    [GeneratedRegex(@"location\s+(?:=\s+|~\s+)?(?<path>\S+)\s*\{(?:[^{}]|\$\{\w+\})*\}")]
    private static partial Regex LocationBlocks();
}
