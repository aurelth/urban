/** Uma cobrança vencida e ainda não paga por inteiro. A API calcula o atraso a cada leitura; o dia do vencimento ainda vale. */
export interface OverdueCharge {
  readonly chargeId: string;
  /** Competência (AAAA-MM). */
  readonly month: string;
  /** AAAA-MM-DD. */
  readonly dueDate: string;
  readonly amount: number;
  readonly paid: number;
  /** O que falta pagar desta cobrança. */
  readonly balance: number;
  /** Dias desde o vencimento (1 no dia seguinte a ele). */
  readonly daysOverdue: number;
}

/** Uma unidade com cobrança em atraso. */
export interface Debtor {
  readonly unitId: string;
  readonly block: string | null;
  readonly number: string;
  /** Quanto a unidade deve no total, somando as cobranças em atraso. */
  readonly balance: number;
  readonly overdueCharges: number;
  /** AAAA-MM-DD do vencimento mais antigo ainda em aberto. */
  readonly oldestDueDate: string;
  readonly daysOverdue: number;
  /** Da cobrança mais antiga para a mais nova. */
  readonly charges: readonly OverdueCharge[];
}

/** `totalOverdue` e `units` valem para todas as unidades devedoras, não só para as da página. */
export interface Delinquency {
  /** AAAA-MM-DD: o dia, em Brasília, a que o cálculo se refere. */
  readonly asOf: string;
  readonly totalOverdue: number;
  readonly units: number;
  readonly page: number;
  readonly pageSize: number;
  /** Da unidade que mais deve para a que menos deve. */
  readonly items: readonly Debtor[];
}

export interface DelinquencyQuery {
  readonly page: number;
  readonly pageSize: number;
}

export const DEFAULT_DELINQUENCY_QUERY: DelinquencyQuery = { page: 1, pageSize: 50 };
