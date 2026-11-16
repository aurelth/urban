namespace Urban.Api.Endpoints;

/// <param name="Pix">Nulo enquanto não há chave cadastrada.</param>
/// <param name="Account">Nulo enquanto não há conta cadastrada.</param>
/// <param name="UpdatedAt">Nulo enquanto o condomínio não cadastrou nada.</param>
public sealed record PaymentInstructionsResponse(PixKeyResponse? Pix, BankAccountResponse? Account, DateTimeOffset? UpdatedAt);

/// <param name="Kind">"cnpj", "cpf", "email", "phone" ou "random".</param>
public sealed record PixKeyResponse(string Kind, string Key);

/// <param name="Kind">"checking" ou "savings".</param>
public sealed record BankAccountResponse(string Bank, string Agency, string Number, string Kind);
