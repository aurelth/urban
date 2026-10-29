namespace Urban.SharedKernel.Abstractions;

/// <summary>
/// Porta de saída da consulta de CEP. Quem responde (BrasilAPI, ViaCEP) é detalhe de infraestrutura e pode ser trocado sem tocar
/// nas regras. Falha de provedor nunca vira exceção: volta como <see cref="PostalCodeLookupStatus.Unavailable"/>.
/// </summary>
public interface IPostalCodeLookup
{
    /// <param name="postalCode">Exatamente oito dígitos, sem pontuação: quem chama normaliza e valida antes.</param>
    /// <exception cref="ArgumentException">O CEP não tem oito dígitos.</exception>
    Task<PostalCodeLookupResult> LookupAsync(string postalCode, CancellationToken cancellationToken);
}
