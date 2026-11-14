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

const BRAZIL_OFFSET_MS = -3 * 3_600_000;

/**
 * Data de saída do vínculo (dd/mm/aaaa): uma data que existe, da entrada (`startDate`, AAAA-MM-DD) até hoje, como na API.
 * Erros: `date` (não existe ou fora do formato), `beforeStart` e `future`. O "hoje" é o de Brasília (UTC-3 o ano todo),
 * que a API usa, não o do UTC nem o do relógio do aparelho. Vazio fica por conta do Validators.required.
 */
export function exitDate(startDate: string, clock: () => Date = () => new Date()): ValidatorFn {
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
    if (!isRealDate(year, month, day)) {
      return { date: true };
    }
    const left = year * 10000 + month * 100 + day;
    if (left < Number(startDate.replaceAll('-', ''))) {
      return { beforeStart: true };
    }
    const brazil = new Date(clock().getTime() + BRAZIL_OFFSET_MS);
    const today =
      brazil.getUTCFullYear() * 10000 + (brazil.getUTCMonth() + 1) * 100 + brazil.getUTCDate();
    return left > today ? { future: true } : null;
  };
}
