using Microsoft.EntityFrameworkCore;
using Urban.Identity.Application.Abstractions;
using Urban.Identity.Application.Administration;
using Urban.Identity.Domain.Access;
using Urban.Identity.Domain.Users;

namespace Urban.Identity.Infrastructure.Persistence;

/// <summary>
/// As contas de síndico para o painel do administrador. A mudança de estado é um único UPDATE condicionado ao estado esperado,
/// que o banco aplica de forma atômica. A busca é feita em SQL porque o e-mail e os nomes têm conversores de valor, e o EF Core
/// não traduz <c>ILIKE</c> sobre eles.
/// </summary>
public sealed class ManagerAccounts(IdentityDbContext db) : IManagerAccounts
{
    private static readonly string SearchSql =
        $"SELECT * FROM \"{IdentityDbContext.Schema}\".\"user\" WHERE email ILIKE {{0}} OR display_name ILIKE {{0}} OR requested_condominium_name ILIKE {{0}}";

    public async Task<ManagerPage> ListAsync(ManagerFilter filter, int page, int pageSize, CancellationToken cancellationToken)
    {
        IQueryable<User> query = Filtered(filter);

        int total = await query.CountAsync(cancellationToken);
        List<User> items = await query
            .OrderByDescending(u => u.CreatedAt)
            .ThenBy(u => u.Id)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync(cancellationToken);

        return new ManagerPage(items, total);
    }

    public async Task<PlatformSummary> SummarizeAsync(CancellationToken cancellationToken)
    {
        List<StatusCount> counts = await db.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Manager)
            .GroupBy(u => u.Status)
            .Select(g => new StatusCount(g.Key, g.Count()))
            .ToListAsync(cancellationToken);

        AccountStatus[] listed = [.. ManagerStatuses.Listed];
        int condominiums = await db.Users
            .AsNoTracking()
            .Where(u => u.Role == UserRole.Manager && u.CondominiumId != null && listed.Contains(u.Status))
            .Select(u => u.CondominiumId)
            .Distinct()
            .CountAsync(cancellationToken);

        return new PlatformSummary(
            CountOf(counts, AccountStatus.Active),
            CountOf(counts, AccountStatus.Suspended),
            CountOf(counts, AccountStatus.PendingVerification),
            CountOf(counts, AccountStatus.PendingApproval),
            condominiums);
    }

    public async Task<bool> TryChangeStatusAsync(User user, AccountStatus expectedStatus, CancellationToken cancellationToken)
    {
        int changed = await db.Users
            .Where(u => u.Id == user.Id && u.Status == expectedStatus)
            .ExecuteUpdateAsync(
                set => set
                    .SetProperty(u => u.Status, user.Status)
                    .SetProperty(u => u.StatusChangedAt, user.StatusChangedAt)
                    .SetProperty(u => u.StatusChangedBy, user.StatusChangedBy)
                    .SetProperty(u => u.StatusReason, user.StatusReason),
                cancellationToken);

        return changed == 1;
    }

    private static int CountOf(List<StatusCount> counts, AccountStatus status) => counts.SingleOrDefault(c => c.Status == status)?.Count ?? 0;

    private IQueryable<User> Filtered(ManagerFilter filter)
    {
        IQueryable<User> users = filter.Search is { } search
            ? db.Users.FromSqlRaw(SearchSql, $"%{EscapeLike(search)}%").AsNoTracking()
            : db.Users.AsNoTracking();

        IQueryable<User> managers = users.Where(u => u.Role == UserRole.Manager);
        if (filter.Status is { } status)
        {
            return managers.Where(u => u.Status == status);
        }

        AccountStatus[] listed = [.. ManagerStatuses.Listed];

        return managers.Where(u => listed.Contains(u.Status));
    }

    /// <summary>Faz % e _ valerem como caracteres comuns na busca (ILIKE os trata como curingas). A barra invertida é o escape padrão do PostgreSQL.</summary>
    private static string EscapeLike(string text) =>
        text.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal);

    private sealed record StatusCount(AccountStatus Status, int Count);
}
