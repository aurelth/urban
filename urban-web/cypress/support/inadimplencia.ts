export const DELINQUENCY_PATH = '/financeiro/inadimplencia';
export const DELINQUENCY_API = '/api/finance/delinquency';

export interface StubOverdueCharge {
  chargeId: string;
  month: string;
  dueDate: string;
  amount: number;
  paid: number;
  balance: number;
  daysOverdue: number;
}

export interface StubDebtor {
  unitId: string;
  block: string | null;
  number: string;
  balance: number;
  overdueCharges: number;
  oldestDueDate: string;
  daysOverdue: number;
  charges: StubOverdueCharge[];
}

const SEPTEMBER: StubOverdueCharge = {
  chargeId: '0199bbbb-0030-7000-8000-000000000011',
  month: '2026-09',
  dueDate: '2026-10-10',
  amount: 800,
  paid: 0,
  balance: 800,
  daysOverdue: 36,
};

const OCTOBER_101: StubOverdueCharge = {
  chargeId: '0199bbbb-0030-7000-8000-000000000101',
  month: '2026-10',
  dueDate: '2026-11-10',
  amount: 800,
  paid: 300,
  balance: 500,
  daysOverdue: 5,
};

const OCTOBER_102: StubOverdueCharge = {
  chargeId: '0199bbbb-0030-7000-8000-000000000102',
  month: '2026-10',
  dueDate: '2026-11-10',
  amount: 800,
  paid: 0,
  balance: 800,
  daysOverdue: 5,
};

const OCTOBER_201: StubOverdueCharge = {
  chargeId: '0199bbbb-0030-7000-8000-000000000201',
  month: '2026-10',
  dueDate: '2026-11-10',
  amount: 400,
  paid: 0,
  balance: 400,
  daysOverdue: 5,
};

export const DEBTOR_101: StubDebtor = {
  unitId: '0199bbbb-0005-7000-8000-000000000101',
  block: 'A',
  number: '101',
  balance: 1300,
  overdueCharges: 2,
  oldestDueDate: '2026-10-10',
  daysOverdue: 36,
  charges: [SEPTEMBER, OCTOBER_101],
};

export const DEBTOR_102: StubDebtor = {
  unitId: '0199bbbb-0005-7000-8000-000000000102',
  block: 'A',
  number: '102',
  balance: 800,
  overdueCharges: 1,
  oldestDueDate: '2026-11-10',
  daysOverdue: 5,
  charges: [OCTOBER_102],
};

export const DEBTOR_201: StubDebtor = {
  unitId: '0199bbbb-0005-7000-8000-000000000201',
  block: null,
  number: '201',
  balance: 400,
  overdueCharges: 1,
  oldestDueDate: '2026-11-10',
  daysOverdue: 5,
  charges: [OCTOBER_201],
};

/** As unidades devedoras (GET), com o total e a paginação que a API faz; o dia do cálculo é 15/11/2026. */
export function stubDelinquency(
  debtors: readonly StubDebtor[] = [DEBTOR_101, DEBTOR_102, DEBTOR_201],
): void {
  cy.intercept({ method: 'GET', pathname: DELINQUENCY_API }, (request) => {
    const params = new URL(request.url).searchParams;
    const page = Number(params.get('page') ?? '1');
    const pageSize = Number(params.get('pageSize') ?? '50');
    request.reply({
      statusCode: 200,
      body: {
        asOf: '2026-11-15',
        totalOverdue: debtors.reduce((sum, debtor) => sum + debtor.balance, 0),
        units: debtors.length,
        page,
        pageSize,
        items: debtors.slice((page - 1) * pageSize, page * pageSize),
      },
    });
  }).as('delinquency');
}

/** O reenvio da fatura de uma cobrança (POST): a API põe na fila e responde 202. */
export function stubResendInvoice(): void {
  cy.intercept('POST', /\/api\/finance\/charges\/[^/]+\/invoice-deliveries$/, {
    statusCode: 202,
  }).as('resend');
}
