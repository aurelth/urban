using FluentAssertions;
using FluentAssertions.Specialized;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace Urban.IntegrationTests.Identity.Persistence;

internal static class ConstraintAssertions
{
    /// <summary>Confere que o banco recusou a gravação pela restrição esperada (e não por outro motivo, como uma tabela ausente).</summary>
    public static async Task ShouldBeRejectedByAsync(this Func<Task> save, string sqlState)
    {
        ExceptionAssertions<DbUpdateException> failure = await save.Should().ThrowAsync<DbUpdateException>();

        failure.Which.InnerException.Should().BeOfType<PostgresException>().Which.SqlState.Should().Be(sqlState);
    }
}
