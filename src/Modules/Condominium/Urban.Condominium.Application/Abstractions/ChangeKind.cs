namespace Urban.Condominium.Application.Abstractions;

/// <summary>Em qual das edições a mudança aconteceu.</summary>
public enum ChangeKind
{
    /// <summary>Campos livres: nome de exibição, celular, endereços e nome do condomínio.</summary>
    Profile,

    /// <summary>Campos sensíveis: nome completo, data de nascimento e CPF. Exigem a senha.</summary>
    Identity,

    /// <summary>Cadastro, edição, desativação ou reativação de uma unidade.</summary>
    Unit,

    /// <summary>Cadastro de várias unidades de uma vez; o registro guarda só a quantidade.</summary>
    UnitBatch,

    /// <summary>Troca do método de rateio das despesas.</summary>
    Apportionment,

    /// <summary>Cadastro ou edição de um morador e dos vínculos dele com as unidades.</summary>
    Resident,

    /// <summary>Cadastro, edição ou exclusão de um veículo de uma unidade.</summary>
    Vehicle,
}
