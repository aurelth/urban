import { SAVED_INSTRUCTIONS, StubInstructions, stubInstructions } from './dados-pagamento';

export const DASHBOARD_PATH = '/dashboard';

/** O painel pede o mês de hoje, que muda com o relógio de quem roda o teste: casa qualquer mês. */
const MONTH_URL = /^\/api\/finance\/months\/\d{4}-\d{2}$/;
const PAYMENTS_URL = /^\/api\/finance\/months\/\d{4}-\d{2}\/payments$/;

type MonthKind = 'open' | 'closed' | 'not-open';

export interface DashboardStub {
  month?: MonthKind;
  needsAmountCount?: number;
  overdueCount?: number;
  /** Cobranças em atraso, quando o mês está fechado. */
  overdueCharges?: number;
  /** Faltam dados em todas as unidades, então o rateio escolhido não está pronto. */
  apportionmentReady?: boolean;
  activeUnits?: number;
  residents?: number;
  instructions?: StubInstructions;
}

const ENTRY = {
  id: '0199bbbb-0003-7000-8000-000000000001',
  expenseId: '0199bbbb-0002-7000-8000-000000000001',
  groupId: '0199bbbb-0001-7000-8000-000000000001',
  description: 'Portaria',
  amount: 4200,
  label: null,
  dueDate: '2026-11-10',
  status: 'pending',
  needsAmount: false,
  suggestedAmount: null,
  version: '21',
};

const CLOSING = {
  closedAt: '2026-11-05T12:00:00+00:00',
  dueDate: '2026-12-10',
  total: 4200,
  basis: 'idealFraction',
  charges: [],
};

function summary(ready: boolean, active: number): Record<string, unknown> {
  return {
    active,
    deactivated: 0,
    houses: 0,
    apartments: active,
    idealFractionTotal: 1,
    withoutIdealFraction: ready ? 0 : active,
    withoutArea: active,
    apportionmentMethod: 'idealFraction',
    apportionmentVersion: '12',
    readiness: [
      { method: 'idealFraction', ready },
      { method: 'equalShare', ready: true },
      { method: 'area', ready: false },
    ],
  };
}

/** Tudo em ordem, com o mês aberto: cada teste muda só o que quer provar. */
export function stubDashboard(options: DashboardStub = {}): void {
  const kind = options.month ?? 'open';
  cy.intercept({ method: 'GET', pathname: MONTH_URL }, (request) => {
    const month = /months\/(\d{4}-\d{2})/.exec(request.url)![1];
    request.reply({
      statusCode: 200,
      body: {
        month,
        entries: kind === 'not-open' ? [] : [{ ...ENTRY, month }],
        groups: [],
        total: kind === 'not-open' ? 0 : 4200,
        needsAmountCount: options.needsAmountCount ?? 0,
        overdueCount: options.overdueCount ?? 0,
        closing: kind === 'closed' ? CLOSING : null,
      },
    });
  }).as('month');
  cy.intercept({ method: 'GET', pathname: PAYMENTS_URL }, (request) => {
    request.reply({
      statusCode: 200,
      body: {
        month: /months\/(\d{4}-\d{2})/.exec(request.url)![1],
        dueDate: '2026-12-10',
        charged: 2400,
        received: 1300,
        total: options.overdueCharges ?? 0,
        page: 1,
        pageSize: 1,
        items: [],
      },
    });
  }).as('payments');
  cy.intercept('GET', '/api/units/summary', {
    statusCode: 200,
    body: summary(options.apportionmentReady ?? true, options.activeUnits ?? 40),
  }).as('unitsSummary');
  cy.intercept(
    { method: 'GET', pathname: '/api/residents' },
    {
      statusCode: 200,
      body: { items: [], total: options.residents ?? 37, page: 1, pageSize: 1 },
    },
  ).as('residents');
  stubInstructions(options.instructions ?? SAVED_INSTRUCTIONS);
}

/** Faz a primeira chamada à API falhar; chame depois do `stubDashboard`, para as seguintes responderem como ele. */
export function failOnce(pathname: string | RegExp, alias: string): void {
  cy.intercept(
    { method: 'GET', pathname, times: 1 },
    { statusCode: 500, body: { status: 500, code: 'erro', detail: 'Detalhe da API' } },
  ).as(alias);
}

export { MONTH_URL, PAYMENTS_URL };
