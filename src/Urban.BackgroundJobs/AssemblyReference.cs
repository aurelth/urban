using System.Reflection;

namespace Urban.BackgroundJobs;

/// <summary>Marcador para localizar este assembly (testes de arquitetura).</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}