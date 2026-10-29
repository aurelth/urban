using Urban.SharedKernel.Abstractions;

namespace Urban.TestSupport.Condominium;

/// <summary>Tenant de teste: o condomínio da "requisição" é o que o teste escolher (nulo = quem ainda não tem condomínio).</summary>
public sealed class FixedTenant(Guid? condominiumId) : ITenantContext
{
    public Guid? CondominiumId { get; } = condominiumId;
}
