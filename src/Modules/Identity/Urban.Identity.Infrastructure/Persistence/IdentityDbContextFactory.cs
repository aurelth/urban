using Microsoft.EntityFrameworkCore.Design;

namespace Urban.Identity.Infrastructure.Persistence;

/// <summary>
/// Usada só pela ferramenta <c>dotnet ef</c> para criar migrations sem subir a aplicação. Gerar uma migration não
/// abre conexão, então a cadeia abaixo é apenas um valor de preenchimento (sem senha).
/// </summary>
internal sealed class IdentityDbContextFactory : IDesignTimeDbContextFactory<IdentityDbContext>
{
    private const string DesignTimeConnectionString = "Host=localhost;Database=urban_design_time";

    public IdentityDbContext CreateDbContext(string[] args) =>
        new(IdentityPersistence.CreateOptions(DesignTimeConnectionString));
}
