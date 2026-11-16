export type ChargeStatus = 'open' | 'partial' | 'paid' | 'overdue';

export type PaymentMethod = 'pix' | 'boleto' | 'transfer' | 'cash' | 'other';

/** Uma cobrança (a fatura de uma unidade no mês fechado) com o que já foi pago. A situação é calculada pela API a cada leitura. */
export interface ChargeSummary {
  readonly chargeId: string;
  readonly unitId: string;
  readonly block: string | null;
  readonly number: string;
  readonly amount: number;
  readonly paid: number;
  readonly balance: number;
  readonly status: ChargeStatus;
  /** AAAA-MM-DD do último recebimento que não foi estornado. */
  readonly lastReceivedOn: string | null;
}

/** `charged` e `received` são do mês inteiro, com ou sem filtro; `total` conta as cobranças do filtro, para a paginação. */
export interface MonthPayments {
  readonly month: string;
  readonly dueDate: string;
  readonly charged: number;
  readonly received: number;
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
  readonly items: readonly ChargeSummary[];
}

export interface Payment {
  readonly id: string;
  readonly chargeId: string;
  readonly amount: number;
  readonly receivedOn: string;
  readonly method: PaymentMethod;
  readonly note: string | null;
  readonly registeredAt: string;
  /** Preenchido quando o pagamento foi estornado: ele continua na lista, mas deixa de contar. */
  readonly reversedAt: string | null;
  readonly reversalReason: string | null;
}

export interface ChargePayments {
  readonly chargeId: string;
  readonly month: string;
  readonly dueDate: string;
  readonly amount: number;
  readonly paid: number;
  readonly balance: number;
  readonly status: ChargeStatus;
  /** Do mais novo para o mais antigo, estornados inclusive. */
  readonly payments: readonly Payment[];
}

export interface RegisterPaymentPayload {
  readonly amount: number;
  /** AAAA-MM-DD. */
  readonly receivedOn: string;
  readonly method: PaymentMethod;
  readonly note: string | null;
}

export type PaymentsStatusFilter = ChargeStatus | 'all';

export interface PaymentsQuery {
  readonly status: PaymentsStatusFilter;
  readonly page: number;
  readonly pageSize: number;
}

export const DEFAULT_PAYMENTS_QUERY: PaymentsQuery = { status: 'all', page: 1, pageSize: 50 };
