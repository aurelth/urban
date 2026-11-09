const THOUSANDS_FORMAT = /^\d{1,3}(\.\d{3})+(,\d+)?$/;
const PLAIN_FORMAT = /^\d+([.,]\d+)?$/;

/**
 * Número digitado em português: "1.234,56", "1234,56" ou "1234.56". Zero ou maior, com no máximo `maxDecimals` casas;
 * qualquer outra coisa (vazio, letras, sinal, casas demais) é nulo. Os limites de cada campo ficam com quem chama.
 */
export function parseDecimal(text: string, maxDecimals: number): number | null {
  const normalized = normalize(text.trim());
  if (normalized === null || decimalsOf(normalized) > maxDecimals) {
    return null;
  }
  return Number(normalized);
}

/** O número como a pessoa o digita: vírgula decimal, sem casas inúteis e com pelo menos `minDecimals` casas. */
export function formatDecimalInput(value: number, minDecimals = 0): string {
  const [integer, decimals = ''] = String(value).split('.');
  return decimals.length === 0 && minDecimals === 0
    ? integer
    : `${integer},${decimals.padEnd(minDecimals, '0')}`;
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
