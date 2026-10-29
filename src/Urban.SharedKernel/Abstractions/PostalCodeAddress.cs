namespace Urban.SharedKernel.Abstractions;

/// <summary>Endereço de um CEP. Rua e bairro vêm vazios nos CEPs gerais de cidades pequenas.</summary>
public sealed record PostalCodeAddress(string PostalCode, string Street, string Neighborhood, string City, string State);
