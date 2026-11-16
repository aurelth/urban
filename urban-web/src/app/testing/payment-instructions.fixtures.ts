import { PaymentInstructions } from '../core/finance/payment-instructions.models';

/** Dados para pagamento já cadastrados: chave Pix de CNPJ e conta corrente. */
export function paymentInstructions(
  overrides: Partial<PaymentInstructions> = {},
): PaymentInstructions {
  return {
    pix: { kind: 'cnpj', key: '11222333000181' },
    account: { bank: 'Banco do Brasil', agency: '1234-5', number: '98765-4', kind: 'checking' },
    updatedAt: '2026-11-16T13:30:00+00:00',
    ...overrides,
  };
}

/** Condomínio que ainda não cadastrou nada. */
export const NO_INSTRUCTIONS: PaymentInstructions = { pix: null, account: null, updatedAt: null };
