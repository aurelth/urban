using Microsoft.EntityFrameworkCore;
using Npgsql;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>
/// Leitura e edição do cadastro do síndico. A versão que a tela carrega é o <c>xmin</c> das duas linhas (condomínio e
/// síndico): o PostgreSQL o troca a cada UPDATE, então uma gravação feita a partir de uma leitura velha encontra outro valor
/// e é recusada. A gravação e o registro de alterações vão no mesmo <c>SaveChanges</c>: ou os dois existem, ou nenhum.
/// </summary>
public sealed class CondominiumProfiles(CondominiumDbContext db) : ICondominiumProfiles
{
    private const string CpfIndex = "ix_person_condominium_id_cpf_hash";
    private const string VersionProperty = "xmin";

    public async Task<ManagerCondominium?> FindOfManagerAsync(Guid managerUserId, CancellationToken cancellationToken)
    {
        // O filtro de tenant do síndico já garante que só o cadastro do condomínio da requisição aparece.
        Person? manager = await db.People.SingleOrDefaultAsync(p => p.UserId == managerUserId, cancellationToken);
        if (manager is null)
        {
            return null;
        }

        CondominiumEntity condominium = await db.Condominiums.SingleAsync(c => c.Id == manager.CondominiumId, cancellationToken);

        return new ManagerCondominium(condominium, manager, CurrentVersion(condominium, manager));
    }

    public async Task<Result<string>> SaveAsync(ManagerCondominium registration, ChangeLogEntry entry, CancellationToken cancellationToken)
    {
        db.Set<ChangeLogRecord>().Add(new ChangeLogRecord
        {
            CondominiumId = entry.CondominiumId,
            ActorUserId = entry.ActorUserId,
            Kind = entry.Kind.ToString().ToLowerInvariant(),
            Fields = [.. entry.Fields],
            ChangedAt = entry.At,
        });

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success(CurrentVersion(registration.Condominium, registration.Manager));
        }
        catch (DbUpdateConcurrencyException)
        {
            db.ChangeTracker.Clear();
            return Result.Failure<string>(CondominiumErrors.ConcurrencyConflict);
        }
        catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: CpfIndex })
        {
            db.ChangeTracker.Clear();
            return Result.Failure<string>(PersonErrors.CpfAlreadyRegistered);
        }
    }

    private string CurrentVersion(CondominiumEntity condominium, Person manager) =>
        $"{db.Entry(condominium).Property<uint>(VersionProperty).CurrentValue}.{db.Entry(manager).Property<uint>(VersionProperty).CurrentValue}";
}
