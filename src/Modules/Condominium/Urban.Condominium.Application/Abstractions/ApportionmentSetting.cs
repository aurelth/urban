using CondominiumEntity = Urban.Condominium.Domain.Condominiums.Condominium;

namespace Urban.Condominium.Application.Abstractions;

/// <summary>O condomínio do tenant e a versão da linha dele, para trocar o método de rateio sem pisar em outra edição.</summary>
public sealed record ApportionmentSetting(CondominiumEntity Condominium, string Version);
