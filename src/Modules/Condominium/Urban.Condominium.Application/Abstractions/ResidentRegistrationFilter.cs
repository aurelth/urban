namespace Urban.Condominium.Application.Abstractions;

/// <summary>Quais moradores a lista traz quanto à completude do cadastro.</summary>
public enum ResidentRegistrationFilter
{
    /// <summary>Todos, com o cadastro completo ou não.</summary>
    All,

    /// <summary>Só adultos sem profissão ou sem contato de emergência (inclui quem fez 18 anos depois de cadastrado como dependente).</summary>
    Incomplete,
}
