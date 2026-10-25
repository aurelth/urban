using System.Reflection;

namespace Urban.EmailDelivery;

/// <summary>Marcador para localizar este assembly (testes de arquitetura).</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}