namespace Urban.Api.Endpoints;

/// <summary>Os campos sensíveis do síndico, com a senha como prova. O CPF ausente mantém o atual; a data vai como AAAA-MM-DD.</summary>
public sealed record UpdateManagerIdentityRequest(string? Version, string? Password, string? FullName, DateOnly? BirthDate, string? Cpf)
{
    /// <summary>Senha e CPF são dados sensíveis: o texto do pedido omite a senha e esconde o CPF.</summary>
    public override string ToString() => $"{nameof(UpdateManagerIdentityRequest)} {{ FullName = {FullName}, Cpf = *, BirthDate = * }}";
}
