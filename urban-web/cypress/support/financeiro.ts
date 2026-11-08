export const EXPENSES_PATH = '/financeiro/despesas';
export const ENTRIES_PATH = '/financeiro/lancamentos';

/** Hoje, para a tela sugerir sempre o mesmo mês: 15 de novembro de 2026. */
export const TODAY = new Date('2026-11-15T15:00:00Z').getTime();

export const ORDINARY_GROUP = {
  id: '0199bbbb-0001-7000-8000-000000000001',
  name: 'Despesas ordinárias',
  version: '5',
};
export const WORKS_GROUP = {
  id: '0199bbbb-0001-7000-8000-000000000002',
  name: 'Obras',
  version: '6',
};

export interface StubExpense {
  id: string;
  groupId: string;
  description: string;
  supplier: string | null;
  kind: 'single' | 'fixed' | 'variable' | 'installment';
  startMonth: string;
  endMonth: string | null;
  lastMonth: string | null;
  currentAmount: number | null;
  installmentCount: number | null;
  currentInstallment: number | null;
  installments: { number: number; month: string; amount: number }[];
  amountChanges: { fromMonth: string; amount: number }[];
  isEnded: boolean;
  version: string;
}

export function stubExpense(overrides: Partial<StubExpense> = {}): StubExpense {
  return {
    id: '0199bbbb-0002-7000-8000-000000000001',
    groupId: ORDINARY_GROUP.id,
    description: 'Portaria',
    supplier: 'Segura Ltda',
    kind: 'fixed',
    startMonth: '2026-09',
    endMonth: null,
    lastMonth: null,
    currentAmount: 4200,
    installmentCount: null,
    currentInstallment: null,
    installments: [],
    amountChanges: [{ fromMonth: '2026-09', amount: 4200 }],
    isEnded: false,
    version: '11',
    ...overrides,
  };
}

export const FIXED_EXPENSE = stubExpense();
export const VARIABLE_EXPENSE = stubExpense({
  id: '0199bbbb-0002-7000-8000-000000000002',
  description: 'Conta de luz',
  supplier: null,
  kind: 'variable',
  currentAmount: null,
  amountChanges: [],
  version: '12',
});
export const INSTALLMENT_EXPENSE = stubExpense({
  id: '0199bbbb-0002-7000-8000-000000000003',
  groupId: WORKS_GROUP.id,
  description: 'Elevador',
  supplier: null,
  kind: 'installment',
  startMonth: '2026-11',
  lastMonth: '2027-04',
  currentAmount: 166.66,
  installmentCount: 6,
  currentInstallment: 1,
  installments: [
    { number: 1, month: '2026-11', amount: 166.66 },
    { number: 2, month: '2026-12', amount: 166.66 },
    { number: 3, month: '2027-01', amount: 166.66 },
    { number: 4, month: '2027-02', amount: 166.66 },
    { number: 5, month: '2027-03', amount: 166.66 },
    { number: 6, month: '2027-04', amount: 166.7 },
  ],
  amountChanges: [],
  version: '13',
});

/** Grupos e despesas. O E2E do CI sobe só o front, sem API. */
export function stubExpenses(
  expenses: StubExpense[] = [FIXED_EXPENSE, VARIABLE_EXPENSE, INSTALLMENT_EXPENSE],
  alias = 'expenses',
): void {
  cy.intercept('POST', '/api/finance/expense-groups/default', {
    statusCode: 200,
    body: ORDINARY_GROUP,
  });
  cy.intercept('GET', '/api/finance/expense-groups', {
    statusCode: 200,
    body: [ORDINARY_GROUP, WORKS_GROUP],
  }).as('groups');
  cy.intercept('GET', /^\/api\/finance\/expenses(\?.*)?$/, { statusCode: 200, body: expenses }).as(
    alias,
  );
}

export function stubFinanceRejected(
  method: 'POST' | 'PUT',
  url: string | RegExp,
  statusCode: number,
  code: string,
  alias: string,
): void {
  cy.intercept(method, url, {
    statusCode,
    body: { status: statusCode, code, detail: 'Detalhe da API' },
  }).as(alias);
}

export interface StubEntry {
  id: string;
  expenseId: string;
  groupId: string;
  month: string;
  description: string;
  amount: number | null;
  label: string | null;
  isPending: boolean;
  suggestedAmount: number | null;
  version: string;
}

export function stubEntry(overrides: Partial<StubEntry> = {}): StubEntry {
  return {
    id: '0199bbbb-0003-7000-8000-000000000001',
    expenseId: FIXED_EXPENSE.id,
    groupId: ORDINARY_GROUP.id,
    month: '2026-11',
    description: 'Portaria',
    amount: 4200,
    label: null,
    isPending: false,
    suggestedAmount: null,
    version: '21',
    ...overrides,
  };
}

export const FIXED_ENTRY = stubEntry();
export const PENDING_ENTRY = stubEntry({
  id: '0199bbbb-0003-7000-8000-000000000002',
  expenseId: VARIABLE_EXPENSE.id,
  description: 'Conta de luz',
  amount: null,
  isPending: true,
  suggestedAmount: 812.4,
  version: '22',
});

export const OPENED_MONTH = {
  month: '2026-11',
  entries: [FIXED_ENTRY, PENDING_ENTRY],
  groups: [
    {
      groupId: ORDINARY_GROUP.id,
      groupName: ORDINARY_GROUP.name,
      total: 4200,
      pendingCount: 1,
    },
  ],
  total: 4200,
  pendingCount: 1,
};

export const EMPTY_MONTH = { month: '2026-11', entries: [], groups: [], total: 0, pendingCount: 0 };

export function stubMonth(month: unknown = OPENED_MONTH, alias = 'month'): void {
  cy.intercept('GET', /^\/api\/finance\/months\/\d{4}-\d{2}$/, { statusCode: 200, body: month }).as(
    alias,
  );
}
