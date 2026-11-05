import { UnitPayload } from '../../core/units/units.models';
import { BatchRow } from './unit-batch.generator';
import { formatPercentInput, parseArea, parsePercent, percentToFraction } from './unit-values';

const NUMBER_MAX_LENGTH = 20;
const BLOCK_MAX_LENGTH = 30;
const FRACTION_DECIMALS = 8;

export const FRACTION_MESSAGE =
  'Informe uma fração maior que 0% e até 100%, com até 6 casas decimais';
export const AREA_MESSAGE =
  'Informe uma área maior que 0 e até 99.999,99 m², com até 2 casas decimais';

/** Problema de cada campo de uma linha; o campo sem problema não aparece. */
export interface RowProblems {
  number?: string;
  block?: string;
  fraction?: string;
  area?: string;
}

export function validateRow(row: BatchRow): RowProblems {
  const problems: RowProblems = {};
  const number = row.number.trim();
  if (number === '') {
    problems.number = 'Informe o número da unidade';
  } else if (number.length > NUMBER_MAX_LENGTH) {
    problems.number = 'O número pode ter até 20 caracteres';
  }
  if (row.block.trim().length > BLOCK_MAX_LENGTH) {
    problems.block = 'O bloco pode ter até 30 caracteres';
  }
  if (!parsePercent(row.fraction).valid) {
    problems.fraction = FRACTION_MESSAGE;
  }
  if (!parseArea(row.area).valid) {
    problems.area = AREA_MESSAGE;
  }
  return problems;
}

/** Posições das linhas cujo bloco e número (sem diferenciar maiúsculas) aparecem mais de uma vez. */
export function findDuplicateRows(rows: readonly BatchRow[]): ReadonlySet<number> {
  const positionsByKey = new Map<string, number[]>();
  rows.forEach((row, position) => {
    const number = row.number.trim();
    if (number !== '') {
      const key = `${row.block.trim().toLowerCase()}\u0000${number.toLowerCase()}`;
      positionsByKey.set(key, [...(positionsByKey.get(key) ?? []), position]);
    }
  });
  const duplicated = [...positionsByKey.values()].filter((positions) => positions.length > 1);
  return new Set(duplicated.flat());
}

/** Linhas válidas para o corpo do envio, na ordem da tela (é a posição que a API devolve nos erros). */
export function toBatchPayload(rows: readonly BatchRow[]): UnitPayload[] {
  return rows.map((row) => {
    const fraction = parsePercent(row.fraction);
    const area = parseArea(row.area);
    const block = row.block.trim();
    return {
      block: block === '' ? null : block,
      number: row.number.trim(),
      type: row.type,
      idealFraction:
        fraction.valid && fraction.value !== null ? percentToFraction(fraction.value) : null,
      area: area.valid ? area.value : null,
    };
  });
}

/**
 * Reparte o que falta para 100% (descontando as unidades já cadastradas e as linhas que já têm fração) entre as
 * linhas sem fração. Arredonda para baixo, então a soma nunca passa de 100%.
 */
export function splitRemainingFraction(rows: readonly BatchRow[], alreadyUsed: number): BatchRow[] {
  const targets = rows.filter((row) => row.fraction.trim() === '').length;
  const filled = rows.reduce((sum, row) => sum + fractionOf(row), 0);
  const share = targets === 0 ? 0 : floorFraction((1 - alreadyUsed - filled) / targets);
  if (share <= 0) {
    return [...rows];
  }
  const text = formatPercentInput(share);
  return rows.map((row) => (row.fraction.trim() === '' ? { ...row, fraction: text } : row));
}

function fractionOf(row: BatchRow): number {
  const parsed = parsePercent(row.fraction);
  return parsed.valid && parsed.value !== null ? percentToFraction(parsed.value) : 0;
}

function floorFraction(value: number): number {
  const scale = 10 ** FRACTION_DECIMALS;
  return Math.floor(Math.round(value * scale * 1e6) / 1e6) / scale;
}
