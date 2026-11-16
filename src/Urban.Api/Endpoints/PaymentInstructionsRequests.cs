namespace Urban.Api.Endpoints;

/// <param name="Pix">Opcional. Ausente ou nulo tira a chave Pix.</param>
/// <param name="Account">Opcional. Ausente ou nula tira a conta bancária.</param>
public sealed record PaymentInstructionsRequest(PixKeyRequest? Pix, BankAccountRequest? Account);

/// <param name="Kind">"cnpj", "cpf", "email", "phone" ou "random".</param>
/// <param name="Key">A chave como a pessoa a digita; a API guarda no formato do banco (CNPJ e CPF sem pontuação, e-mail em minúsculas, celular com +55).</param>
public sealed record PixKeyRequest(string? Kind, string? Key);

/// <param name="Bank">O nome do banco, de 2 a 60 caracteres.</param>
/// <param name="Agency">Até 5 dígitos, com dígito verificador opcional depois de um hífen.</param>
/// <param name="Number">A conta: até 12 dígitos, com dígito verificador opcional depois de um hífen.</param>
/// <param name="Kind">"checking" (corrente) ou "savings" (poupança).</param>
public sealed record BankAccountRequest(string? Bank, string? Agency, string? Number, string? Kind);
