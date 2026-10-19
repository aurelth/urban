using System.Reflection;

namespace Urban.ArchTests;

internal static class Estrutura
{
    public static readonly string[] Modulos = ["Identidade", "Condominio", "Financeiro", "Operacoes"];

    public static readonly string[] Camadas = ["Domain", "Application", "Infrastructure"];

    public static Assembly Carregar(string modulo, string camada) => Assembly.Load($"Urban.{modulo}.{camada}");

    public static IEnumerable<string> Referencias(Assembly assembly) =>
        assembly.GetReferencedAssemblies().Select(a => a.Name ?? string.Empty);
}
