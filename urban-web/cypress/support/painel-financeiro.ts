export const MONTH_DASHBOARD_PATH = '/financeiro/painel';
export const MONTH_DASHBOARD_API = /\/api\/finance\/months\/(\d{4}-\d{2})\/dashboard$/;

const ORDINARY = {
  groupId: '0199cccc-0060-7000-8000-000000000001',
  groupName: 'Despesas ordinárias',
  total: 1800,
  needsAmountCount: 0,
};
const WORKS = {
  groupId: '0199cccc-0060-7000-8000-000000000002',
  groupName: 'Obras',
  total: 1200,
  needsAmountCount: 0,
};

/** Novembro: fechado, com uma unidade paga, uma parcial e uma atrasada. */
export const NOVEMBER_DASHBOARD = {
  month: '2026-11',
  asOf: '2026-11-15',
  closed: true,
  expenses: {
    total: 3000,
    needsAmountCount: 0,
    overdueCount: 0,
    groups: [ORDINARY, WORKS],
  },
  charges: {
    dueDate: '2026-11-10',
    charged: 2400,
    received: 1300,
    balance: 1100,
    units: 3,
    open: 0,
    partial: 1,
    paid: 1,
    overdue: 1,
    overdueAmount: 800,
  },
};

/** Outubro: fechado e quitado. */
export const OCTOBER_DASHBOARD = {
  month: '2026-10',
  asOf: '2026-11-15',
  closed: true,
  expenses: { total: 2400, needsAmountCount: 0, overdueCount: 0, groups: [ORDINARY] },
  charges: {
    dueDate: '2026-10-10',
    charged: 2400,
    received: 2400,
    balance: 0,
    units: 3,
    open: 0,
    partial: 0,
    paid: 3,
    overdue: 0,
    overdueAmount: 0,
  },
};

/** Dezembro: aberto, com lançamentos esperando valor, ainda sem cobranças. */
export const DECEMBER_DASHBOARD = {
  month: '2026-12',
  asOf: '2026-11-15',
  closed: false,
  expenses: {
    total: 1800,
    needsAmountCount: 2,
    overdueCount: 0,
    groups: [{ ...ORDINARY, needsAmountCount: 2 }],
  },
  charges: null,
};

function notOpened(month: string): unknown {
  return {
    month,
    asOf: '2026-11-15',
    closed: false,
    expenses: { total: 0, needsAmountCount: 0, overdueCount: 0, groups: [] },
    charges: null,
  };
}

const BY_MONTH: Readonly<Record<string, unknown>> = {
  '2026-10': OCTOBER_DASHBOARD,
  '2026-11': NOVEMBER_DASHBOARD,
  '2026-12': DECEMBER_DASHBOARD,
};

/** O painel de cada mês (GET); mês que não está na lista volta como nunca aberto. Hoje é 15/11/2026. */
export function stubMonthDashboard(): void {
  cy.intercept({ method: 'GET', url: MONTH_DASHBOARD_API }, (request) => {
    const month = MONTH_DASHBOARD_API.exec(request.url)![1];
    request.reply({ statusCode: 200, body: BY_MONTH[month] ?? notOpened(month) });
  }).as('dashboard');
}

/** Faz a primeira leitura do painel falhar; chame depois do `stubMonthDashboard`, para as seguintes responderem como ele. */
export function failMonthDashboardOnce(): void {
  cy.intercept(
    { method: 'GET', url: MONTH_DASHBOARD_API, times: 1 },
    { statusCode: 500, body: { status: 500, code: 'erro', detail: 'Detalhe da API' } },
  ).as('dashboardFailure');
}
