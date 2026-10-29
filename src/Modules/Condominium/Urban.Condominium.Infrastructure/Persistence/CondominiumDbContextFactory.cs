using System.Security.Cryptography;
using Microsoft.EntityFrameworkCore.Design;
using Urban.Condominium.Infrastructure.Security;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>
/// Usada só pela ferramenta <c>dotnet ef</c> para criar migrations sem subir a aplicação. Gerar uma migration não
/// abre conexão nem cifra nada, então a cadeia e as chaves abaixo são apenas valores de preenchimento, sem segredo.
/// </summary>
internal sealed class CondominiumDbContextFactory : IDesignTimeDbContextFactory<CondominiumDbContext>
{
    private const string DesignTimeConnectionString = "Host=localhost;Database=urban_design_time";

    public CondominiumDbContext CreateDbContext(string[] args)
    {
        PersonalDataOptions keys = new()
        {
            EncryptionKeys = { ["v1"] = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)) },
            HashKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)),
        };

        return new CondominiumDbContext(
            CondominiumPersistence.CreateOptions(DesignTimeConnectionString),
            new PersonalDataProtector(keys),
            new NoTenant());
    }

    private sealed class NoTenant : ITenantContext
    {
        public Guid? CondominiumId => null;
    }
}
