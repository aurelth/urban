using MediatR;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.PostalCodes;

/// <summary>O CEP chega como o usuário digitou (com ou sem pontuação); o Domain valida e normaliza.</summary>
public sealed record LookupPostalCodeQuery(string? PostalCode) : IRequest<Result<PostalCodeAddress>>;
