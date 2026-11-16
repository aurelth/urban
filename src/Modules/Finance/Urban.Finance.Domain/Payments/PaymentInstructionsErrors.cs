using Urban.SharedKernel;

namespace Urban.Finance.Domain.Payments;

public static class PaymentInstructionsErrors
{
    public static readonly Error PixKeyKindInvalid = new("finance.payment_instructions.pix_kind_invalid", "Escolha o tipo da chave Pix: CNPJ, CPF, e-mail, celular ou chave aleatória");

    public static readonly Error PixKeyInvalid = new("finance.payment_instructions.pix_key_invalid", "A chave Pix não é válida para o tipo escolhido");

    public static readonly Error BankInvalid = new("finance.payment_instructions.bank_invalid", "Informe o nome do banco, em uma linha só, com 2 a 60 caracteres");

    public static readonly Error AgencyInvalid = new("finance.payment_instructions.agency_invalid", "A agência deve ter até 5 dígitos, com dígito verificador opcional depois de um hífen");

    public static readonly Error NumberInvalid = new("finance.payment_instructions.account_number_invalid", "A conta deve ter até 12 dígitos, com dígito verificador opcional depois de um hífen");

    public static readonly Error AccountKindInvalid = new("finance.payment_instructions.account_kind_invalid", "Escolha o tipo da conta: corrente ou poupança");
}
