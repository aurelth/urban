namespace Urban.Condominium.Application.Abstractions;

/// <summary>
/// Registro de auditoria de uma edição: quem mudou, quando e QUAIS campos. Nunca os valores (o CPF, por exemplo, não pode
/// aparecer em lugar nenhum fora da coluna cifrada), então a tabela nem tem coluna que os comporte.
/// </summary>
public sealed record ChangeLogEntry(Guid CondominiumId, Guid ActorUserId, ChangeKind Kind, IReadOnlyList<string> Fields, DateTimeOffset At);
