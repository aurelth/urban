/** Resultado da leitura de um campo numérico opcional: vazio é válido e vira nulo. */
export type NumberInput =
  { readonly valid: true; readonly value: number | null } | { readonly valid: false };

const PERCENT_DECIMALS = 6;
const AREA_DECIMALS = 2;
const FRACTION_DECIMALS = 8;
const MAX_PERCENT = 100;
const MAX_AREA = 99999.99;

const THOUSANDS_FORMAT = /^\d{1,3}(\.\d{3})+(,\d+)?$/;
const PLAIN_FORMAT = /^\d+([.,]\d+)?$/;
const TRAILING_ZEROS = /\.?0+$/;
const PERCENT_SUFFIX = /\s*%$/;
const AREA_SUFFIX = /\s*m[²2]$/i;

const areaFormatter = new Intl.NumberFormat('pt-BR', { maximumFractionDigits: AREA_DECIMALS });

/** Porcentagem digitada: "1,25", "1.25" ou "1,25%". De 0 (exclusive) a 100, com até 6 casas. */
export function parsePercent(text: string): NumberInput {
  return parseBounded(text, PERCENT_SUFFIX, PERCENT_DECIMALS, MAX_PERCENT);
}

/** Área digitada em m²: "52,5", "1.250,50" ou "80 m²". De 0 (exclusive) a 99.999,99, com até 2 casas. */
export function parseArea(text: string): NumberInput {
  return parseBounded(text, AREA_SUFFIX, AREA_DECIMALS, MAX_AREA);
}

/** Porcentagem (1,25) para a fração que a API guarda (0,0125), sem sobra de ponto flutuante. */
export function percentToFraction(percent: number): number {
  return Math.round(percent * 10 ** PERCENT_DECIMALS) / 10 ** FRACTION_DECIMALS;
}

export function fractionToPercent(fraction: number): number {
  return Math.round(fraction * 10 ** FRACTION_DECIMALS) / 10 ** PERCENT_DECIMALS;
}

/** Como a fração aparece num campo editável: "1,25", ou vazio quando não há. */
export function formatPercentInput(fraction: number | null): string {
  return fraction === null ? '' : toDecimalComma(fractionToPercent(fraction), PERCENT_DECIMALS);
}

/** Como a fração aparece na lista: "1,25%", ou "—" quando não há. */
export function formatPercent(fraction: number | null): string {
  return fraction === null ? '—' : `${formatPercentInput(fraction)}%`;
}

export function formatAreaInput(area: number | null): string {
  return area === null ? '' : toDecimalComma(area, AREA_DECIMALS);
}

export function formatArea(area: number | null): string {
  return area === null ? '—' : `${areaFormatter.format(area)} m²`;
}

/** "Bloco A · 101", ou só o número quando a unidade não tem bloco. */
export function unitLabel(unit: {
  readonly block: string | null;
  readonly number: string;
}): string {
  return unit.block ? `Bloco ${unit.block} · ${unit.number}` : unit.number;
}

function parseBounded(
  raw: string,
  suffix: RegExp,
  maxDecimals: number,
  maximum: number,
): NumberInput {
  const typed = raw.trim();
  if (typed === '') {
    return { valid: true, value: null };
  }
  const text = typed.replace(suffix, '');
  const normalized = text === '' ? null : normalize(text);
  if (normalized === null || decimalsOf(normalized) > maxDecimals) {
    return { valid: false };
  }
  const value = Number(normalized);
  return value > 0 && value <= maximum ? { valid: true, value } : { valid: false };
}

/** Ponto seguido de exatamente 3 dígitos é separador de milhar ("1.250"); nos demais casos, é decimal. */
function normalize(text: string): string | null {
  if (THOUSANDS_FORMAT.test(text)) {
    return text.replaceAll('.', '').replace(',', '.');
  }
  return PLAIN_FORMAT.test(text) ? text.replace(',', '.') : null;
}

function decimalsOf(normalized: string): number {
  const dot = normalized.indexOf('.');
  return dot === -1 ? 0 : normalized.length - dot - 1;
}

function toDecimalComma(value: number, decimals: number): string {
  return value.toFixed(decimals).replace(TRAILING_ZEROS, '').replace('.', ',');
}
