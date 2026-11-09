export const TARIFFS_PATH = '/financeiro/tarifas';
export const TARIFFS_API = '/api/finance/consumption-tariffs';
export const SETTINGS_API = '/api/finance/settings';

export interface StubBand {
  upToCubicMeters: number | null;
  fixedAmount: number;
  pricePerCubicMeter: number;
}

export interface StubTariff {
  validFrom: string;
  water: StubBand[];
  sewage: StubBand[] | null;
  sewagePercent: number;
  gas: { kilosPerCubicMeter: number; pricePerKilo: number; pricePerCubicMeter: number } | null;
}

/** Em vigor em novembro de 2026, o mês de TODAY: água em 3 faixas, esgoto com metade do valor e gás. */
export const CURRENT_TARIFF: StubTariff = {
  validFrom: '2026-11',
  water: [
    { upToCubicMeters: 10, fixedAmount: 28.41, pricePerCubicMeter: 0 },
    { upToCubicMeters: 20, fixedAmount: 0, pricePerCubicMeter: 3.5 },
    { upToCubicMeters: null, fixedAmount: 0, pricePerCubicMeter: 5.1 },
  ],
  sewage: [{ upToCubicMeters: null, fixedAmount: 12.5, pricePerCubicMeter: 2 }],
  sewagePercent: 50,
  gas: { kilosPerCubicMeter: 2.4, pricePerKilo: 15.71, pricePerCubicMeter: 37.7 },
};

export const OLD_TARIFF: StubTariff = {
  validFrom: '2026-03',
  water: [{ upToCubicMeters: null, fixedAmount: 25, pricePerCubicMeter: 4.8 }],
  sewage: null,
  sewagePercent: 100,
  gas: null,
};

/** Tarifas (da mais nova para a mais antiga) e o percentual do fundo de reserva. O E2E do CI sobe só o front, sem API. */
export function stubTariffs(
  tariffs: StubTariff[] = [CURRENT_TARIFF, OLD_TARIFF],
  reserveFundPercent = 5,
): void {
  cy.intercept('GET', TARIFFS_API, { statusCode: 200, body: tariffs }).as('tariffs');
  cy.intercept('GET', SETTINGS_API, { statusCode: 200, body: { reserveFundPercent } }).as(
    'settings',
  );
}

/** Um campo de uma faixa do diálogo: `section` é `water` ou `sewage`, `row` começa em 0. */
export function bandField(
  section: 'water' | 'sewage',
  row: number,
  name: 'upTo' | 'fixed' | 'price',
): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy
    .get(`urban-tariff-dialog [data-testid="${section}-bands"] [data-testid="band-row"]`)
    .eq(row)
    .find(`[formControlName="${name}"]`);
}
