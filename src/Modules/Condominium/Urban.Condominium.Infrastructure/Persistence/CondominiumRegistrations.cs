using Microsoft.EntityFrameworkCore;
using Npgsql;
using Urban.Condominium.Application.Abstractions;
using Urban.Condominium.Domain.Condominiums;
using Urban.Condominium.Domain.Documents;
using Urban.Condominium.Domain.People;
using Urban.SharedKernel;
using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.Condominium.Infrastructure.Persistence;

public sealed class CondominiumRegistrations(CondominiumDbContext db) : ICondominiumRegistrations
{
    // Os nomes vêm das migrations; as conferências antes de gravar são só gentileza, quem garante a unicidade é o índice.
    private const string CnpjIndex = "ix_condominium_cnpj";
    private const string ManagerOfCondominiumIndex = "ix_condominium_created_by";
    private const string PersonOfUserIndex = "ix_person_user_id";

    public Task<bool> CnpjExistsAsync(Cnpj cnpj, CancellationToken cancellationToken) =>
        db.Condominiums.AnyAsync(c => c.Cnpj == cnpj, cancellationToken);

    public Task<bool> ManagerHasCondominiumAsync(Guid managerUserId, CancellationToken cancellationToken) =>
        db.Condominiums.AnyAsync(c => c.CreatedBy == managerUserId, cancellationToken);

    public async Task<Result> SaveAsync(CondominiumEntity condominium, Person manager, CancellationToken cancellationToken)
    {
        db.Condominiums.Add(condominium);
        db.People.Add(manager);

        try
        {
            await db.SaveChangesAsync(cancellationToken);
            return Result.Success();
        }
        catch (DbUpdateException ex) when (ConflictFrom(ex) is { } conflict)
        {
            db.ChangeTracker.Clear();
            return Result.Failure(conflict);
        }
    }

    private static Error? ConflictFrom(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } violation
            ? violation.ConstraintName switch
            {
                CnpjIndex => CondominiumErrors.CnpjAlreadyRegistered,
                ManagerOfCondominiumIndex or PersonOfUserIndex => CondominiumErrors.ManagerAlreadyRegistered,
                _ => null,
            }
            : null;
}
