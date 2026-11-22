export const MY_INVOICE_PATH = '/minha-fatura';
export const MY_MONTH_API = /\/api\/finance\/me\/months\/(\d{4}-\d{2})$/;

const ORDINARY = {
  groupId: '0199dddd-0072-7000-8000-000000000001',
  groupName: 'Despesas ordinárias',
  total: 1800,
};
const WORKS = {
  groupId: '0199dddd-0072-7000-8000-000000000002',
  groupName: 'Obras',
  total: 1200,
};

export const OVERDUE_CHARGE_ID = '0199dddd-0070-7000-8000-000000000001';
export const PAID_CHARGE_ID = '0199dddd-0070-7000-8000-000000000002';

/** Novembro: fechado, com duas unidades da pessoa: a 101 atrasada e o A-102 paga. */
export const NOVEMBER_MINE = {
  month: '2026-11',
  asOf: '2026-11-15',
  access: 'unit',
  closed: true,
  units: [
    {
      unitId: '0199dddd-0071-7000-8000-000000000001',
      block: null,
      number: '101',
      charge: {
        chargeId: OVERDUE_CHARGE_ID,
        dueDate: '2026-11-10',
        amount: 800,
        paid: 300,
        balance: 500,
        status: 'overdue',
        lastReceivedOn: '2026-11-05',
      },
    },
    {
      unitId: '0199dddd-0071-7000-8000-000000000002',
      block: 'A',
      number: '102',
      charge: {
        chargeId: PAID_CHARGE_ID,
        dueDate: '2026-11-10',
        amount: 650.5,
        paid: 650.5,
        balance: 0,
        status: 'paid',
        lastReceivedOn: '2026-11-08',
      },
    },
  ],
  expenses: { total: 3000, groups: [ORDINARY, WORKS] },
};

/** Dezembro: ainda aberto. */
export const DECEMBER_MINE = {
  month: '2026-12',
  asOf: '2026-11-15',
  access: 'unit',
  closed: false,
  units: [
    {
      unitId: '0199dddd-0071-7000-8000-000000000001',
      block: null,
      number: '101',
      charge: null,
    },
  ],
  expenses: null,
};

export const DEPENDENT_MINE = {
  month: '2026-11',
  asOf: '2026-11-15',
  access: 'dependent',
  closed: true,
  units: [],
  expenses: null,
};

export const NO_UNIT_MINE = { ...DEPENDENT_MINE, access: 'none' };

/** O mês do morador (GET): novembro e dezembro prontos; outro mês volta como ainda aberto. Hoje é 15/11/2026. */
export function stubMyMonth(overrides: Readonly<Record<string, unknown>> = {}): void {
  const byMonth: Readonly<Record<string, unknown>> = {
    '2026-11': NOVEMBER_MINE,
    '2026-12': DECEMBER_MINE,
    ...overrides,
  };
  cy.intercept({ method: 'GET', url: MY_MONTH_API }, (request) => {
    const month = MY_MONTH_API.exec(request.url)![1];
    request.reply({
      statusCode: 200,
      body: byMonth[month] ?? { ...DECEMBER_MINE, month },
    });
  }).as('myMonth');
}

/** O download da própria fatura (GET): um PDF mínimo com o nome sugerido pela API. */
export function stubMyInvoice(chargeId: string, fileName: string): void {
  cy.intercept('GET', `/api/finance/me/charges/${chargeId}/invoice`, {
    statusCode: 200,
    headers: {
      'content-type': 'application/pdf',
      'content-disposition': `attachment; filename=${fileName}; filename*=UTF-8''${fileName}`,
    },
    body: '%PDF-1.7\n% fatura de teste do Urban\n',
  }).as('myInvoice');
}

/** A API recusa o download com um problema (404, 429...): o corpo chega como arquivo, como na API de verdade. */
export function stubMyInvoiceRejected(chargeId: string, status: number, code: string): void {
  cy.intercept('GET', `/api/finance/me/charges/${chargeId}/invoice`, {
    statusCode: status,
    headers: { 'content-type': 'application/problem+json' },
    body: JSON.stringify({ status, code, detail: 'Detalhe da API' }),
  }).as('myInvoice');
}
