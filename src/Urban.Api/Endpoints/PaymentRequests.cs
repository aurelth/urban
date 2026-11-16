namespace Urban.Api.Endpoints;

/// <param name="Amount">O valor recebido, em reais, com até 2 casas decimais.</param>
/// <param name="ReceivedOn">O dia em que o dinheiro chegou, como AAAA-MM-DD.</param>
/// <param name="Method">"pix", "boleto", "transfer", "cash" ou "other".</param>
/// <param name="Note">Opcional, uma linha de até 200 caracteres.</param>
public sealed record RegisterPaymentRequest(decimal? Amount, string? ReceivedOn, string? Method, string? Note);

/// <param name="Reason">Por que o pagamento está sendo estornado; obrigatório, uma linha de até 200 caracteres.</param>
public sealed record ReversePaymentRequest(string? Reason);
