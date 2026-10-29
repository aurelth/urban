namespace Urban.Api.Endpoints;

/// <summary>Endereço do CEP, para a tela preencher o formulário. Rua e bairro vêm vazios nos CEPs gerais de cidades pequenas.</summary>
public sealed record PostalCodeResponse(string PostalCode, string Street, string Neighborhood, string City, string State);
