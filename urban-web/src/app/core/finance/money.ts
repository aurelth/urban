import { AbstractControl, ValidationErrors } from '@angular/forms';

const MAX_AMOUNT = 9999999.99;
const DECIMALS = 2;

const THOUSANDS_FORMAT = /^\d{1,3}(\.\d{3})+(,\d+)?$/;
const PLAIN_FORMAT = /^\d+([.,]\d+)?$/;
const CURRENCY_PREFIX = /^r\$\s*/i;

const formatter = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

/** Valor digitado: "1.234,56", "1234,56", "1234.56" ou "R$ 1.234,56". Maior que zero, até 9.999.999,99, com até 2 casas. */
export function parseMoney(text: string): number | null {
  const normalized = normalize(text.trim().replace(CURRENCY_PREFIX, ''));
  if (normalized === null || decimalsOf(normalized) > DECIMALS) {
    return null;
  }
  const value = Number(normalized);
  return value > 0 && value <= MAX_AMOUNT ? value : null;
}

export function formatMoney(amount: number | null): string {
  return amount === null ? '—' : formatter.format(amount);
}

export function formatMoneyInput(amount: number | null): string {
  return amount === null ? '' : amount.toFixed(DECIMALS).replace('.', ',');
}

export function moneyField(control: AbstractControl<string>): ValidationErrors | null {
  return parseMoney(control.value) === null ? { money: true } : null;
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
