import { FIRST_UNIT, SECOND_UNIT } from './unidades';

export const VEHICLES_PATH = '/veiculos';

export interface StubVehicle {
  id: string;
  unitId: string;
  unitBlock: string | null;
  unitNumber: string;
  plate: string;
  model: string;
  color: string;
  version: string;
}

export function stubVehicle(overrides: Partial<StubVehicle> = {}): StubVehicle {
  return {
    id: '0199dddd-0000-7000-8000-000000000001',
    unitId: FIRST_UNIT.id,
    unitBlock: FIRST_UNIT.block,
    unitNumber: FIRST_UNIT.number,
    plate: 'ABC1D23',
    model: 'Fiat Argo',
    color: 'Prata',
    version: '21',
    ...overrides,
  };
}

export const FIRST_VEHICLE = stubVehicle();
export const SECOND_VEHICLE = stubVehicle({
  id: '0199dddd-0000-7000-8000-000000000002',
  unitId: SECOND_UNIT.id,
  unitBlock: SECOND_UNIT.block,
  unitNumber: SECOND_UNIT.number,
  plate: 'XYZ9876',
  model: 'Honda CG 160',
  color: 'Vermelha',
  version: '22',
});

const LIST_URL = /^\/api\/vehicles(\?.*)?$/;

/** A lista de veículos; o E2E do CI sobe só o front, sem API. As unidades vêm de `stubUnits`. */
export function stubVehicles(
  items: StubVehicle[] = [FIRST_VEHICLE, SECOND_VEHICLE],
  alias = 'vehicles',
): void {
  cy.intercept('GET', LIST_URL, {
    statusCode: 200,
    body: { items, total: items.length, page: 1, pageSize: 50 },
  }).as(alias);
}

export function stubVehicleRejected(
  method: 'POST' | 'PUT' | 'DELETE',
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
