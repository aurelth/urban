export type ExpenseKind = 'single' | 'fixed' | 'variable' | 'installment';

export type ExpenseStatusFilter = 'active' | 'ended' | 'all';

/** Situação de pagamento de um lançamento. "overdue" não se envia: a API calcula (pendente depois do vencimento). */
export type EntryStatus = 'pending' | 'paid' | 'overdue' | 'canceled';

/** As situações que a pessoa pode marcar. */
export type SettableEntryStatus = Exclude<EntryStatus, 'overdue'>;

export interface ExpenseGroup {
  readonly id: string;
  readonly name: string;
  /** Verdadeiro quando o grupo faz parte da base sobre a qual se calcula o fundo de reserva. */
  readonly reserveFundBase: boolean;
  readonly version: string;
}

/** O que a pessoa escolhe ao criar ou editar um grupo. */
export interface ExpenseGroupValues {
  readonly name: string;
  readonly reserveFundBase: boolean;
}

export interface Installment {
  readonly number: number;
  readonly month: string;
  readonly amount: number;
}

export interface AmountChange {
  readonly fromMonth: string;
  readonly amount: number;
}

/** Despesa como a API devolve: os meses vão como "AAAA-MM" e a versão volta em toda alteração. */
export interface Expense {
  readonly id: string;
  readonly groupId: string;
  readonly description: string;
  readonly supplier: string | null;
  readonly kind: ExpenseKind;
  readonly startMonth: string;
  readonly endMonth: string | null;
  readonly lastMonth: string | null;
  readonly currentAmount: number | null;
  /** Dia do mês em que vence (1 a 31). Nos meses mais curtos vale o último dia. */
  readonly dueDay: number;
  readonly installmentCount: number | null;
  readonly currentInstallment: number | null;
  readonly installments: readonly Installment[];
  readonly amountChanges: readonly AmountChange[];
  readonly isEnded: boolean;
  readonly version: string;
}

export interface ExpenseEntry {
  readonly id: string;
  readonly expenseId: string;
  readonly groupId: string;
  readonly month: string;
  readonly description: string;
  readonly amount: number | null;
  readonly label: string | null;
  /** "AAAA-MM-DD". Fica como foi calculado quando o lançamento nasceu. */
  readonly dueDate: string;
  readonly status: EntryStatus;
  /** Verdadeiro enquanto o valor de uma despesa variável não foi informado. */
  readonly needsAmount: boolean;
  readonly suggestedAmount: number | null;
  readonly version: string;
}

export interface GroupTotal {
  readonly groupId: string;
  readonly groupName: string;
  readonly total: number;
  readonly needsAmountCount: number;
}

/** Em que o rateio do condomínio se baseia: o nome do método escolhido na tela de unidades. */
export type ApportionmentBasis = 'idealFraction' | 'equalShare' | 'area';

/** De onde vem cada linha do recibo da unidade. */
export type ChargeLineKind = 'expenses' | 'reserveFund' | 'water' | 'sewage' | 'gas';

/** Uma linha do recibo, com o texto e os números de quando o mês fechou. */
export interface ChargeLine {
  readonly kind: ChargeLineKind;
  readonly label: string;
  readonly amount: number;
  /** Consumo em m³ das linhas de água, esgoto e gás; nulo nas demais. */
  readonly quantity: number | null;
  readonly previousReading: number | null;
  readonly currentReading: number | null;
}

/** O que uma unidade paga num mês fechado. Bloco, número e peso são cópias de quando o mês fechou. */
export interface UnitCharge {
  readonly unitId: string;
  readonly block: string | null;
  readonly number: string;
  /** A fração ideal (0 a 1), a área em m² ou 1 quando todas pagam igual. */
  readonly weight: number;
  /** A soma das linhas. */
  readonly amount: number;
  readonly lines: readonly ChargeLine[];
}

export interface MonthClosing {
  /** Instante do fechamento, em ISO 8601. */
  readonly closedAt: string;
  /** "AAAA-MM-DD". O vencimento de todas as cobranças do mês. */
  readonly dueDate: string;
  /** A soma das despesas do mês. Com fundo de reserva e consumo, as cobranças somam mais que isso. */
  readonly total: number;
  readonly basis: ApportionmentBasis;
  readonly charges: readonly UnitCharge[];
}

export interface MonthEntries {
  readonly month: string;
  readonly entries: readonly ExpenseEntry[];
  readonly groups: readonly GroupTotal[];
  readonly total: number;
  readonly needsAmountCount: number;
  readonly overdueCount: number;
  /** Nulo enquanto o mês está aberto. */
  readonly closing: MonthClosing | null;
}

/** Corpo de POST /api/finance/expenses. Os campos que o tipo não usa vão nulos. */
export interface ExpensePayload {
  readonly kind: ExpenseKind;
  readonly groupId: string;
  readonly description: string;
  readonly supplier: string | null;
  readonly startMonth: string;
  readonly endMonth: string | null;
  readonly amount: number | null;
  readonly installmentCount: number | null;
  readonly currentInstallment: number | null;
  readonly dueDay: number;
}

export interface UpdateExpensePayload {
  readonly version: string;
  readonly description: string;
  readonly supplier: string | null;
  readonly groupId: string;
  readonly dueDay: number;
}
