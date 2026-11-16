export type PixKeyKind = 'cnpj' | 'cpf' | 'email' | 'phone' | 'random';

export type BankAccountKind = 'checking' | 'savings';

export interface PixKey {
  readonly kind: PixKeyKind;
  /** Como a API a guarda: CNPJ e CPF sem pontuação, e-mail em minúsculas e celular com +55. */
  readonly key: string;
}

export interface BankAccount {
  readonly bank: string;
  readonly agency: string;
  readonly number: string;
  readonly kind: BankAccountKind;
}

/** Os dados para o morador pagar a fatura. `pix` e `account` são nulos enquanto não houver cadastro. */
export interface PaymentInstructions {
  readonly pix: PixKey | null;
  readonly account: BankAccount | null;
  readonly updatedAt: string | null;
}

/** A conta como o formulário a envia: com o tipo ainda em branco ela chega incompleta e a API a recusa. */
export interface BankAccountInput {
  readonly bank: string;
  readonly agency: string;
  readonly number: string;
  readonly kind: BankAccountKind | '';
}

/** Nulo tira a chave ou a conta. */
export interface PaymentInstructionsPayload {
  readonly pix: PixKey | null;
  readonly account: BankAccountInput | null;
}
