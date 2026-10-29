using Urban.SharedKernel.Abstractions;

namespace Urban.PostalCodeLookup;

/// <summary>
/// Pergunta ao provedor principal e, se ele não achou o endereço (por falha ou por "não existe"), confirma com o de reserva uma vez.
/// Quando nenhum acha, vale a resposta do reserva: se o principal disse "não existe" e o reserva está fora do ar, o resultado é
/// "indisponível", porque o "não existe" do principal pode ter sido uma falha dele.
/// </summary>
public sealed class FallbackPostalCodeLookup(IPostalCodeLookup primary, IPostalCodeLookup backup) : IPostalCodeLookup
{
    public async Task<PostalCodeLookupResult> LookupAsync(string postalCode, CancellationToken cancellationToken)
    {
        PostalCodeLookupResult first = await primary.LookupAsync(postalCode, cancellationToken);

        return first.Status == PostalCodeLookupStatus.Found
            ? first
            : await backup.LookupAsync(postalCode, cancellationToken);
    }
}
