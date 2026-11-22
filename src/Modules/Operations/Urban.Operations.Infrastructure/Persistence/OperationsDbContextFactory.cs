using Microsoft.EntityFrameworkCore.Design;
using Urban.SharedKernel.Abstractions;

namespace Urban.Operations.Infrastructure.Persistence;

/// <summary>
/// Usada só pela ferramenta <c>dotnet ef</c> para criar migrations sem subir a aplicação. Gerar uma migration não abre
/// conexão, então a cadeia abaixo é apenas um valor de preenchimento, sem segredo.
/// </summary>
internal sealed class OperationsDbContextFactory : IDesignTimeDbContextFactory<OperationsDbContext>
{
    private const string DesignTimeConnectionString = "Host=localhost;Database=urban_design_time";

    public OperationsDbContext CreateDbContext(string[] args) =>
        new(OperationsPersistence.CreateOptions(DesignTimeConnectionString), new NoTenant());

    private sealed class NoTenant : ITenantContext
    {
        public Guid? CondominiumId => null;
    }
}
