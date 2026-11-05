import { UnitKind } from '../../core/units/units.models';

/** Mesmo limite da API: o lote é gravado inteiro ou não é gravado. */
export const MAX_BATCH_UNITS = 500;

const MIN_FLOOR = 0;
const MAX_FLOOR = 99;
const MAX_PER_FLOOR = 99;
const FLOOR_FACTOR = 100;
const NUMBER_WIDTH = 3;

/** Quantas unidades há em um andar. Andares diferentes podem ter quantidades diferentes. */
export interface FloorPlan {
  readonly floor: number;
  readonly count: number;
}

/** Uma linha da pré-visualização do lote, com os textos como aparecem nos campos. */
export interface BatchRow {
  readonly block: string;
  readonly number: string;
  readonly type: UnitKind;
  readonly fraction: string;
  readonly area: string;
}

/** Do primeiro ao último andar, com a mesma quantidade em cada um. Vazio quando o intervalo ou a quantidade não servem. */
export function buildFloors(first: number, last: number, perFloor: number): FloorPlan[] {
  const valid =
    isWhole(first, MIN_FLOOR, MAX_FLOOR) &&
    isWhole(last, MIN_FLOOR, MAX_FLOOR) &&
    isWhole(perFloor, 1, MAX_PER_FLOOR) &&
    first <= last;
  if (!valid) {
    return [];
  }
  return Array.from({ length: last - first + 1 }, (_, index) => ({
    floor: first + index,
    count: perFloor,
  }));
}

/** Andar 1, posição 1 → "101"; andar 12, posição 3 → "1203"; térreo → "001". */
export function floorUnitNumber(floor: number, position: number): string {
  return String(floor * FLOOR_FACTOR + position).padStart(NUMBER_WIDTH, '0');
}

export function generateFloorRows(block: string, floors: readonly FloorPlan[]): BatchRow[] {
  const cleanBlock = block.trim();
  return floors.flatMap(({ floor, count }) =>
    Array.from({ length: Math.max(count, 0) }, (_, index) =>
      row(cleanBlock, floorUnitNumber(floor, index + 1), 'apartment'),
    ),
  );
}

export function generateHouseRows(block: string, count: number): BatchRow[] {
  if (!isWhole(count, 1, MAX_BATCH_UNITS)) {
    return [];
  }
  const cleanBlock = block.trim();
  return Array.from({ length: count }, (_, index) => row(cleanBlock, String(index + 1), 'house'));
}

export function totalUnits(floors: readonly FloorPlan[]): number {
  return floors.reduce((sum, { count }) => sum + count, 0);
}

function row(block: string, number: string, type: UnitKind): BatchRow {
  return { block, number, type, fraction: '', area: '' };
}

function isWhole(value: number, minimum: number, maximum: number): boolean {
  return Number.isInteger(value) && value >= minimum && value <= maximum;
}
