import { AbstractControl, ValidationErrors } from '@angular/forms';
import { parseDecimal } from './decimal';

const MAX_AMOUNT = 9999999.99;
const DECIMALS = 2;

const CURRENCY_PREFIX = /^r\$\s*/i;

const formatter = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

/** Valor digitado: "1.234,56", "1234,56", "1234.56" ou "R$ 1.234,56". Maior que zero, até 9.999.999,99, com até 2 casas. */
export function parseMoney(text: string): number | null {
  const value = parseDecimal(text.trim().replace(CURRENCY_PREFIX, ''), DECIMALS);
  return value !== null && value > 0 && value <= MAX_AMOUNT ? value : null;
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
