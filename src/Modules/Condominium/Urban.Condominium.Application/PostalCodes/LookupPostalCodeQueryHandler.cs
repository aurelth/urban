using MediatR;
using Urban.Condominium.Domain.Contacts;
using Urban.SharedKernel;
using Urban.SharedKernel.Abstractions;

namespace Urban.Condominium.Application.PostalCodes;

/// <summary>Valida o CEP antes de qualquer chamada externa e traduz a resposta dos provedores para os erros do módulo.</summary>
public sealed class LookupPostalCodeQueryHandler(IPostalCodeLookup lookup)
    : IRequestHandler<LookupPostalCodeQuery, Result<PostalCodeAddress>>
{
    public async Task<Result<PostalCodeAddress>> Handle(LookupPostalCodeQuery request, CancellationToken cancellationToken)
    {
        Result<PostalCode> postalCode = PostalCode.Create(request.PostalCode);
        if (postalCode.IsFailure)
        {
            return Result.Failure<PostalCodeAddress>(postalCode.Error);
        }

        PostalCodeLookupResult found = await lookup.LookupAsync(postalCode.Value.Value, cancellationToken);

        return found.Status switch
        {
            PostalCodeLookupStatus.Found => Result.Success(found.Address!),
            PostalCodeLookupStatus.NotFound => Result.Failure<PostalCodeAddress>(PostalCodeErrors.NotFound),
            _ => Result.Failure<PostalCodeAddress>(PostalCodeErrors.LookupUnavailable),
        };
    }
}
