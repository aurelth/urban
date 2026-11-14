import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { isRealDate } from '../../shared/forms/masks';

const FIRST_YEAR = 1900;

/**
 * Data de entrada no vínculo (dd/mm/aaaa): uma data que existe, de 1900 até 1 ano à frente, como na API (`date` quando
 * não vale). O relógio entra de fora para o teste fixar o dia de hoje. Vazio fica por conta do Validators.required.
 */
export function entryDate(clock: () => Date = () => new Date()): ValidatorFn {
  return (control: AbstractControl<string>): ValidationErrors | null => {
    const value = control.value;
    if (value === '') {
      return null;
    }
    const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(value);
    if (!match) {
      return { date: true };
    }
    const [day, month, year] = [Number(match[1]), Number(match[2]), Number(match[3])];
    if (!isRealDate(year, month, day) || year < FIRST_YEAR) {
      return { date: true };
    }
    const now = clock();
    const entered = year * 10000 + month * 100 + day;
    const latest = (now.getFullYear() + 1) * 10000 + (now.getMonth() + 1) * 100 + now.getDate();
    return entered > latest ? { date: true } : null;
  };
}
