namespace Urban.Identity.Domain.Access;

/// <summary>Quem age ou sofre a ação: identificador, papel e condomínio (nulo só para o SuperAdmin).</summary>
public sealed record AccessSubject(Guid UserId, UserRole Role, Guid? CondominiumId);
