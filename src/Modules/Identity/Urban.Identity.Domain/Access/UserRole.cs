namespace Urban.Identity.Domain.Access;

/// <summary>Papel do usuário no sistema. O síndico (<see cref="Manager"/>) também usa tudo o que o morador usa.</summary>
public enum UserRole
{
    Resident = 0,
    Manager = 1,
    SuperAdmin = 2,
}
