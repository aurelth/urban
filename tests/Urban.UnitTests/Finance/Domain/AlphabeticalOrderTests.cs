using FluentAssertions;
using Urban.Finance.Domain;

namespace Urban.UnitTests.Finance.Domain;

/// <summary>A ordem dos nomes em listas e no demonstrativo: a mesma em qualquer máquina, sem depender da cultura do sistema.</summary>
public class AlphabeticalOrderTests
{
    [Fact]
    public void Comparer_IgnoresCaseAndAccents()
    {
        string[] sorted = [.. new[] { "Bomba", "água", "Zincagem", "Áreas comuns", "alvenaria" }.OrderBy(name => name, AlphabeticalOrder.Comparer)];

        sorted.Should().Equal("água", "alvenaria", "Áreas comuns", "Bomba", "Zincagem");
    }

    [Fact]
    public void Comparer_BreaksTiesDeterministically()
    {
        string[] sorted = [.. new[] { "limpeza", "Limpeza" }.OrderBy(name => name, AlphabeticalOrder.Comparer)];

        sorted.Should().Equal("Limpeza", "limpeza");
        AlphabeticalOrder.Comparer.Compare("Limpeza", "Limpeza").Should().Be(0);
    }

    [Fact]
    public void Comparer_TreatsNullAsEmpty()
    {
        AlphabeticalOrder.Comparer.Compare(null, "A").Should().BeNegative();
    }
}
