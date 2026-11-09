export const READINGS_PATH = '/financeiro/leituras';

export function readingsApi(month = '2026-11'): string {
  return `/api/finance/months/${month}/readings`;
}

export interface StubMeterReading {
  previous: number;
  current: number;
  consumption: number;
}

export interface StubUnitReadings {
  unitId: string;
  block: string | null;
  number: string;
  water: StubMeterReading | null;
  gas: StubMeterReading | null;
  suggestedWaterPrevious: number | null;
  suggestedGasPrevious: number | null;
}

export interface StubMonthReadings {
  month: string;
  isClosed: boolean;
  hasTariff: boolean;
  hasGas: boolean;
  units: StubUnitReadings[];
}

/** Bloco A: a 101 sem leitura (com sugestão do mês passado), a 102 já lida e a 201 sem bloco e sem sugestão. */
export const UNIT_101: StubUnitReadings = {
  unitId: '0199bbbb-0005-7000-8000-000000000101',
  block: 'A',
  number: '101',
  water: null,
  gas: null,
  suggestedWaterPrevious: 100,
  suggestedGasPrevious: 7,
};

export const UNIT_102: StubUnitReadings = {
  unitId: '0199bbbb-0005-7000-8000-000000000102',
  block: 'A',
  number: '102',
  water: { previous: 50, current: 60, consumption: 10 },
  gas: null,
  suggestedWaterPrevious: 50,
  suggestedGasPrevious: null,
};

export const UNIT_201: StubUnitReadings = {
  unitId: '0199bbbb-0005-7000-8000-000000000201',
  block: null,
  number: '201',
  water: null,
  gas: null,
  suggestedWaterPrevious: null,
  suggestedGasPrevious: null,
};

export const OPEN_READINGS: StubMonthReadings = {
  month: '2026-11',
  isClosed: false,
  hasTariff: true,
  hasGas: false,
  units: [UNIT_101, UNIT_102, UNIT_201],
};

/** As leituras do mês (GET). A gravação (PUT) é combinada em cada teste. */
export function stubReadings(data: StubMonthReadings = OPEN_READINGS, month = '2026-11'): void {
  cy.intercept('GET', readingsApi(month), { statusCode: 200, body: data }).as(
    month === '2026-11' ? 'readings' : `readings-${month}`,
  );
}

/** Um campo de uma unidade: `row` começa em 0 e `name` é waterPrevious, waterCurrent, gasPrevious ou gasCurrent. */
export function readingField(row: number, name: string): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="unit-row"]').eq(row).find(`[formControlName="${name}"]`);
}
