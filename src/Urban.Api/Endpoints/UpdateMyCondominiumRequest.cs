namespace Urban.Api.Endpoints;

/// <summary>
/// Os campos livres do cadastro. Não há campo para CNPJ, CPF, nome completo ou data de nascimento: se vierem no corpo, são
/// ignorados. Telefone e CEP vão só com dígitos (a máscara é da tela, mas aceitamos os dois).
/// </summary>
/// <param name="Version">A versão lida em <c>GET /api/condominiums/mine</c>.</param>
public sealed record UpdateMyCondominiumRequest(
    string? Version,
    string? DisplayName,
    string? Phone,
    AddressRequest? ManagerAddress,
    string? CondominiumName,
    AddressRequest? CondominiumAddress);
