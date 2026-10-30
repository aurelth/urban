namespace Urban.Condominium.Application.Abstractions;

/// <summary>Em qual das duas edições a mudança aconteceu.</summary>
public enum ChangeKind
{
    /// <summary>Campos livres: nome de exibição, celular, endereços e nome do condomínio.</summary>
    Profile,

    /// <summary>Campos sensíveis: nome completo, data de nascimento e CPF. Exigem a senha.</summary>
    Identity,
}
