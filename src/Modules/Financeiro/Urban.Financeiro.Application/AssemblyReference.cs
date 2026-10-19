using System.Reflection;

namespace Urban.Financeiro.Application;

/// <summary>Marcador para localizar este assembly (injeção de dependência, MediatR, testes de arquitetura).</summary>
public static class AssemblyReference
{
    public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
}
