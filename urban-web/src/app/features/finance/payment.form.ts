import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { parseMoney } from '../../core/finance/money';
import { addMonths } from '../../core/finance/month';
import { PaymentMethod, RegisterPaymentPayload } from '../../core/finance/payments.models';
import { isRealDate, unmask } from '../../shared/forms/masks';

const BRAZIL_OFFSET_MS = -3 * 3_600_000;
const MONTHS_BEFORE_COMPETENCE = 12;
const DATE_TEXT = /^(\d{2})\/(\d{2})\/(\d{4})$/;

export interface RegisterPaymentFormValue {
  readonly amount: string;
  readonly receivedOn: string;
  readonly method: PaymentMethod;
  readonly note: string;
}

/** O dia de hoje no relógio de Brasília (UTC-3 o ano todo), como a API conta, em dd/mm/aaaa. */
export function todayText(now: Date): string {
  const brazil = new Date(now.getTime() + BRAZIL_OFFSET_MS);
  const day = String(brazil.getUTCDate()).padStart(2, '0');
  const month = String(brazil.getUTCMonth() + 1).padStart(2, '0');
  return `${day}/${month}/${brazil.getUTCFullYear()}`;
}

/**
 * Data do recebimento (dd/mm/aaaa): não pode ser no futuro nem anterior ao primeiro dia de 12 meses antes da competência,
 * como a API. Erros: `date` (não existe ou fora do formato), `future` e `tooOld`. Vazio fica por conta do required.
 */
export function paymentDate(competence: string, clock: () => Date): ValidatorFn {
  return (control: AbstractControl<string>): ValidationErrors | null => {
    if (control.value === '') {
      return null;
    }
    const day = dayNumber(control.value);
    if (day === null) {
      return { date: true };
    }
    if (day > dayNumber(todayText(clock()))!) {
      return { future: true };
    }
    const earliest = Number(
      `${addMonths(competence, -MONTHS_BEFORE_COMPETENCE).replace('-', '')}01`,
    );
    return day < earliest ? { tooOld: true } : null;
  };
}

/** O valor não pode passar do saldo da cobrança. Valor vazio ou ilegível fica para o validador de dinheiro. Erro: `exceeds`. */
export function amountUpTo(balance: number): ValidatorFn {
  return (control: AbstractControl<string>): ValidationErrors | null => {
    const amount = parseMoney(control.value);
    return amount !== null && amount > balance ? { exceeds: true } : null;
  };
}

export function registerPayload(value: RegisterPaymentFormValue): RegisterPaymentPayload {
  const note = value.note.trim();
  return {
    amount: parseMoney(value.amount)!,
    receivedOn: unmask('date', value.receivedOn),
    method: value.method,
    note: note === '' ? null : note,
  };
}

/** dd/mm/aaaa → aaaammdd para comparar; nulo quando não é uma data que existe. */
function dayNumber(text: string): number | null {
  const match = DATE_TEXT.exec(text);
  if (match === null) {
    return null;
  }
  const [day, month, year] = [Number(match[1]), Number(match[2]), Number(match[3])];
  return isRealDate(year, month, day) ? year * 10000 + month * 100 + day : null;
}
