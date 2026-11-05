export const UNITS_PATH = '/unidades';
export const BATCH_PATH = '/unidades/lote';

export interface StubUnit {
  id: string;
  block: string | null;
  number: string;
  type: 'apartment' | 'house';
  idealFraction: number | null;
  area: number | null;
  isActive: boolean;
  deactivatedAt: string | null;
  version: string;
}

export function stubUnit(overrides: Partial<StubUnit> = {}): StubUnit {
  return {
    id: '0199aaaa-0000-7000-8000-000000000001',
    block: 'A',
    number: '101',
    type: 'apartment',
    idealFraction: 0.0125,
    area: 52.5,
    isActive: true,
    deactivatedAt: null,
    version: '41',
    ...overrides,
  };
}

export const FIRST_UNIT = stubUnit();
export const SECOND_UNIT = stubUnit({
  id: '0199aaaa-0000-7000-8000-000000000002',
  number: '102',
  idealFraction: null,
  area: null,
  version: '42',
});
export const DEACTIVATED_UNIT = stubUnit({
  id: '0199aaaa-0000-7000-8000-000000000003',
  block: 'B',
  number: '201',
  isActive: false,
  deactivatedAt: '2026-11-01T10:00:00Z',
  version: '43',
});

export function stubSummary(overrides: Record<string, unknown> = {}): Record<string, unknown> {
  return {
    active: 2,
    deactivated: 1,
    houses: 0,
    apartments: 2,
    idealFractionTotal: 0.0125,
    withoutIdealFraction: 1,
    withoutArea: 1,
    apportionmentMethod: 'idealFraction',
    apportionmentVersion: '12',
    readiness: [
      { method: 'idealFraction', ready: false },
      { method: 'equalShare', ready: true },
      { method: 'area', ready: false },
    ],
    ...overrides,
  };
}

const LIST_URL = /^\/api\/units(\?.*)?$/;

/** Lista e resumo das unidades. O E2E do CI sobe só o front, sem API. */
export function stubUnits(
  items: StubUnit[] = [FIRST_UNIT, SECOND_UNIT],
  summary: Record<string, unknown> = stubSummary(),
  alias = 'units',
): void {
  cy.intercept('GET', LIST_URL, {
    statusCode: 200,
    body: { items, total: items.length, page: 1, pageSize: 50 },
  }).as(alias);
  cy.intercept('GET', '/api/units/summary', { statusCode: 200, body: summary }).as('unitsSummary');
}

export function stubUnitRejected(
  method: 'POST' | 'PUT',
  url: string | RegExp,
  statusCode: number,
  code: string,
  alias: string,
  extra: Record<string, unknown> = {},
): void {
  cy.intercept(method, url, {
    statusCode,
    body: { status: statusCode, code, detail: 'Detalhe da API', ...extra },
  }).as(alias);
}
