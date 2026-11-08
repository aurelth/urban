export type ExpenseKind = 'single' | 'fixed' | 'variable' | 'installment';

export type ExpenseStatusFilter = 'active' | 'ended' | 'all';

export interface ExpenseGroup {
  readonly id: string;
  readonly name: string;
  readonly version: string;
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
  readonly isPending: boolean;
  readonly suggestedAmount: number | null;
  readonly version: string;
}

export interface GroupTotal {
  readonly groupId: string;
  readonly groupName: string;
  readonly total: number;
  readonly pendingCount: number;
}

export interface MonthEntries {
  readonly month: string;
  readonly entries: readonly ExpenseEntry[];
  readonly groups: readonly GroupTotal[];
  readonly total: number;
  readonly pendingCount: number;
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
}

export interface UpdateExpensePayload {
  readonly version: string;
  readonly description: string;
  readonly supplier: string | null;
  readonly groupId: string;
}
