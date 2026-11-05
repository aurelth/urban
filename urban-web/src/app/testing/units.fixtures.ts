import { Unit, UnitPage, UnitsSummary } from '../core/units/units.models';

let sequence = 0;

/** Unidade ativa de teste; cada chamada ganha um id novo, e o número acompanha, se não for informado. */
export function unit(overrides: Partial<Unit> = {}): Unit {
  sequence += 1;
  return {
    id: `0199aaaa-0000-7000-8000-${String(sequence).padStart(12, '0')}`,
    block: 'A',
    number: String(100 + sequence),
    type: 'apartment',
    idealFraction: 0.0125,
    area: 52.5,
    isActive: true,
    deactivatedAt: null,
    version: String(sequence),
    ...overrides,
  };
}

export function unitPage(items: Unit[], total = items.length, page = 1, pageSize = 50): UnitPage {
  return { items, total, page, pageSize };
}

export function unitsSummary(overrides: Partial<UnitsSummary> = {}): UnitsSummary {
  return {
    active: 40,
    deactivated: 2,
    houses: 0,
    apartments: 40,
    idealFractionTotal: 0.5,
    withoutIdealFraction: 20,
    withoutArea: 40,
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
