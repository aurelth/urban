using Urban.Finance.Application.Abstractions;
using Urban.Finance.Domain;
using Urban.Finance.Domain.Expenses;
using Urban.Finance.Domain.Months;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.UnitTests.Finance.Application;

internal sealed class FixedClock(DateTimeOffset now) : IClock
{
    public DateTimeOffset UtcNow { get; } = now;
}

/// <summary>Grupos em memória. Cada gravação sobe a versão em 1, como o xmin do banco faria.</summary>
internal sealed class FakeExpenseGroups : IExpenseGroups
{
    private readonly Dictionary<Guid, Stored> _store = [];

    public Action? BeforeAdd { get; set; }

    public IReadOnlyList<ExpenseGroup> All => [.. _store.Values.Select(stored => stored.Group)];

    public ExpenseGroup Existing(ExpenseGroup group, int version = 5)
    {
        _store[group.Id] = new Stored(group, version);

        return group;
    }

    public Task<IReadOnlyList<ExpenseGroupRecord>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExpenseGroupRecord>>(
            [.. _store.Values.OrderBy(stored => stored.Group.Name, StringComparer.OrdinalIgnoreCase).Select(Record)]);

    public Task<ExpenseGroupRecord?> FindAsync(Guid groupId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.TryGetValue(groupId, out Stored? stored) ? Record(stored) : null);

    public Task<bool> IsNameTakenAsync(string name, Guid? exceptGroupId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Values.Any(stored => stored.Group.Id != exceptGroupId && Same(stored.Group.Name, name)));

    public Task<Result<string>> AddAsync(ExpenseGroup group, CancellationToken cancellationToken)
    {
        BeforeAdd?.Invoke();

        if (_store.Values.Any(stored => Same(stored.Group.Name, group.Name)))
        {
            return Task.FromResult(Result.Failure<string>(ExpenseGroupErrors.NameAlreadyExists));
        }

        _store[group.Id] = new Stored(group, 1);

        return Task.FromResult(Result.Success("1"));
    }

    public Task<Result<string>> SaveAsync(ExpenseGroupRecord record, CancellationToken cancellationToken)
    {
        if (_store.Values.Any(stored => stored.Group.Id != record.Group.Id && Same(stored.Group.Name, record.Group.Name)))
        {
            return Task.FromResult(Result.Failure<string>(ExpenseGroupErrors.NameAlreadyExists));
        }

        Stored current = _store[record.Group.Id];
        current.Version++;

        return Task.FromResult(Result.Success(current.Version.ToString()));
    }

    private static bool Same(string left, string right) => string.Equals(left, right, StringComparison.OrdinalIgnoreCase);

    private static ExpenseGroupRecord Record(Stored stored) => new(stored.Group, stored.Version.ToString());

    private sealed class Stored(ExpenseGroup group, int version)
    {
        public ExpenseGroup Group { get; } = group;

        public int Version { get; set; } = version;
    }
}

internal sealed class FakeExpenses : IExpenses
{
    private readonly Dictionary<Guid, Stored> _store = [];

    public IReadOnlyList<Expense> All => [.. _store.Values.Select(stored => stored.Expense)];

    public List<ExpenseRecord> Saved { get; } = [];

    public Error? AddFailure { get; set; }

    public Expense Existing(Expense expense, int version = 5)
    {
        _store[expense.Id] = new Stored(expense, version);

        return expense;
    }

    public Task<IReadOnlyList<ExpenseRecord>> ListAsync(CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExpenseRecord>>(
            [.. _store.Values.OrderBy(stored => stored.Expense.Description, StringComparer.OrdinalIgnoreCase).Select(Record)]);

    public Task<ExpenseRecord?> FindAsync(Guid expenseId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.TryGetValue(expenseId, out Stored? stored) ? Record(stored) : null);

    public Task<Result<string>> AddAsync(Expense expense, CancellationToken cancellationToken)
    {
        if (AddFailure is { } failure)
        {
            return Task.FromResult(Result.Failure<string>(failure));
        }

        _store[expense.Id] = new Stored(expense, 1);

        return Task.FromResult(Result.Success("1"));
    }

    public Task<Result<string>> SaveAsync(ExpenseRecord record, CancellationToken cancellationToken)
    {
        Saved.Add(record);
        Stored current = _store[record.Expense.Id];
        current.Version++;

        return Task.FromResult(Result.Success(current.Version.ToString()));
    }

    private static ExpenseRecord Record(Stored stored) => new(stored.Expense, stored.Version.ToString());

    private sealed class Stored(Expense expense, int version)
    {
        public Expense Expense { get; } = expense;

        public int Version { get; set; } = version;
    }
}

internal sealed class FakeExpenseEntries : IExpenseEntries
{
    private readonly List<Stored> _store = [];

    public IReadOnlyList<ExpenseEntry> All => [.. _store.Select(stored => stored.Entry)];

    public List<ExpenseEntry> Added { get; } = [];

    public Error? AddRangeFailure { get; set; }

    public ExpenseEntry Existing(ExpenseEntry entry, int version = 5)
    {
        _store.Add(new Stored(entry, version));

        return entry;
    }

    public Task<IReadOnlyList<ExpenseEntryRecord>> ListAsync(YearMonth month, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExpenseEntryRecord>>([.. _store.Where(stored => stored.Entry.Month == month).Select(Record)]);

    public Task<IReadOnlyList<ExpenseEntryRecord>> ListForExpenseFromAsync(Guid expenseId, YearMonth from, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<ExpenseEntryRecord>>(
            [.. _store.Where(stored => stored.Entry.ExpenseId == expenseId && stored.Entry.Month >= from).Select(Record)]);

    public Task<ExpenseEntryRecord?> FindAsync(Guid entryId, CancellationToken cancellationToken) =>
        Task.FromResult(_store.Find(stored => stored.Entry.Id == entryId) is { } stored ? Record(stored) : null);

    public Task<Result> AddRangeAsync(IReadOnlyList<ExpenseEntry> entries, CancellationToken cancellationToken)
    {
        if (AddRangeFailure is { } failure)
        {
            return Task.FromResult(Result.Failure(failure));
        }

        bool duplicated = entries.Any(entry => _store.Any(stored => stored.Entry.ExpenseId == entry.ExpenseId && stored.Entry.Month == entry.Month));

        if (duplicated)
        {
            return Task.FromResult(Result.Failure(FinanceErrors.ConcurrencyConflict));
        }

        foreach (ExpenseEntry entry in entries)
        {
            _store.Add(new Stored(entry, 1));
            Added.Add(entry);
        }

        return Task.FromResult(Result.Success());
    }

    public Task<Result<string>> SaveAsync(ExpenseEntryRecord record, CancellationToken cancellationToken)
    {
        Stored current = _store.Single(stored => stored.Entry.Id == record.Entry.Id);
        current.Version++;

        return Task.FromResult(Result.Success(current.Version.ToString()));
    }

    private static ExpenseEntryRecord Record(Stored stored) => new(stored.Entry, stored.Version.ToString());

    private sealed class Stored(ExpenseEntry entry, int version)
    {
        public ExpenseEntry Entry { get; } = entry;

        public int Version { get; set; } = version;
    }
}
