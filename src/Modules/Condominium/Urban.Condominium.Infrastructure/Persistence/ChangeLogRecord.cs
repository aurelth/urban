using Urban.Condominium.Application.Abstractions;

namespace Urban.Condominium.Infrastructure.Persistence;

/// <summary>
/// Linha do registro de alterações. Guarda quem, quando e quais campos mudaram; não há coluna para valores, de propósito:
/// o registro pode ser lido por quem opera o sistema sem expor CPF, nome ou endereço.
/// </summary>
internal sealed class ChangeLogRecord
{
    public Guid Id { get; init; } = Guid.CreateVersion7();

    public Guid CondominiumId { get; init; }

    public Guid ActorUserId { get; init; }

    /// <summary>O que foi mudado, quando não é o cadastro do síndico: a unidade, por exemplo. Nulo nas demais.</summary>
    public Guid? SubjectId { get; init; }

    public required string Kind { get; init; }

    public required string[] Fields { get; init; }

    public DateTimeOffset ChangedAt { get; init; }

    public static ChangeLogRecord From(ChangeLogEntry entry) =>
        new()
        {
            CondominiumId = entry.CondominiumId,
            ActorUserId = entry.ActorUserId,
            SubjectId = entry.SubjectId,
            Kind = entry.Kind.ToString().ToLowerInvariant(),
            Fields = [.. entry.Fields],
            ChangedAt = entry.At,
        };
}
