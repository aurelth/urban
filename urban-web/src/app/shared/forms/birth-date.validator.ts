import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';
import { isRealDate } from './masks';

export const MIN_AGE = 18;
export const MAX_AGE = 120;

interface CalendarDay {
  year: number;
  month: number;
  day: number;
}

/**
 * Data de nascimento do síndico no formato dd/mm/aaaa: `date` quando não existe, é futura ou tem
 * mais de 120 anos; `underAge` quando ainda não tem 18. Mesma regra da API. O relógio entra de
 * fora para o teste fixar o dia de hoje. Vazio fica por conta do Validators.required.
 */
export function birthDate(clock: () => Date = () => new Date()): ValidatorFn {
  return (control: AbstractControl<string>): ValidationErrors | null => {
    const value = control.value;
    if (value === '') {
      return null;
    }
    const born = parse(value);
    if (!born) {
      return { date: true };
    }
    const now = clock();
    const today: CalendarDay = {
      year: now.getFullYear(),
      month: now.getMonth() + 1,
      day: now.getDate(),
    };
    if (isAfter(born, today) || isAfter(shiftYears(today, -MAX_AGE), born)) {
      return { date: true };
    }
    return isAfter(eighteenthBirthday(born), today) ? { underAge: true } : null;
  };
}

function parse(text: string): CalendarDay | null {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(text);
  if (!match) {
    return null;
  }
  const [year, month, day] = [Number(match[3]), Number(match[2]), Number(match[1])];
  return isRealDate(year, month, day) ? { year, month, day } : null;
}

function isAfter(a: CalendarDay, b: CalendarDay): boolean {
  return a.year * 10000 + a.month * 100 + a.day > b.year * 10000 + b.month * 100 + b.day;
}

/** Quem nasceu em 29 de fevereiro faz 18 anos em 1º de março, porque 18 anos depois não é bissexto. */
function eighteenthBirthday(born: CalendarDay): CalendarDay {
  return born.month === 2 && born.day === 29
    ? { year: born.year + MIN_AGE, month: 3, day: 1 }
    : shiftYears(born, MIN_AGE);
}

/** Soma anos levando 29 de fevereiro para 28 quando o ano de destino não é bissexto. */
function shiftYears(day: CalendarDay, years: number): CalendarDay {
  const year = day.year + years;
  return day.month === 2 && day.day === 29 && !isRealDate(year, 2, 29)
    ? { year, month: 2, day: 28 }
    : { ...day, year };
}
