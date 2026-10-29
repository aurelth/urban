using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using Urban.Condominium.Infrastructure.Persistence;
using Urban.Identity.Infrastructure.Persistence;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Api.Persistence;

/// <summary>
/// Uma transação do PostgreSQL só para os módulos que gravam juntos. Cada módulo tem o seu DbContext, mas todos apontam
/// para o mesmo banco: o contexto do Identity abre a conexão e a transação, e o do Condominium empresta as duas. Assim
/// as gravações dos dois módulos são confirmadas ou desfeitas juntas, sem transação distribuída e sem um módulo conhecer o outro.
/// Mora na API (a raiz de composição) porque é o único projeto que enxerga os dois módulos.
/// </summary>
public sealed class AtomicWork(IdentityDbContext identity, CondominiumDbContext condominium) : IAtomicWork
{
    public async Task<Result> ExecuteAsync(Func<CancellationToken, Task<Result>> work, CancellationToken cancellationToken)
    {
        DbContext[] guests = [condominium];
        string?[] ownConnectionStrings = [.. guests.Select(guest => guest.Database.GetConnectionString())];

        await using IDbContextTransaction transaction = await identity.Database.BeginTransactionAsync(cancellationToken);
        DbConnection connection = identity.Database.GetDbConnection();
        DbTransaction shared = transaction.GetDbTransaction();

        try
        {
            foreach (DbContext guest in guests)
            {
                guest.Database.SetDbConnection(connection);
                await guest.Database.UseTransactionAsync(shared, cancellationToken);
            }

            Result result = await work(cancellationToken);
            if (result.IsSuccess)
            {
                await transaction.CommitAsync(cancellationToken);
            }
            else
            {
                await transaction.RollbackAsync(cancellationToken);
            }

            return result;
        }
        finally
        {
            // Trocar a conexão também larga a transação emprestada; com a cadeia de conexão de volta, cada convidado abre a sua própria conexão.
            for (int i = 0; i < guests.Length; i++)
            {
                guests[i].Database.SetDbConnection(null);
                guests[i].Database.SetConnectionString(ownConnectionStrings[i]);
            }
        }
    }
}
