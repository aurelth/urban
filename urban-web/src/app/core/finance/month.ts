const MIN_YEAR = 2000;
const MAX_YEAR = 2200;
const BRAZIL_OFFSET_HOURS = -3;
const MONTH_FORMAT = /^(\d{4})-(0[1-9]|1[0-2])$/;

const titleFormatter = new Intl.DateTimeFormat('pt-BR', {
  month: 'long',
  year: 'numeric',
  timeZone: 'UTC',
});

export interface MonthOption {
  readonly value: string;
  readonly label: string;
}

/** O mês de hoje no relógio de Brasília (UTC-3 o ano todo), como a API conta. */
export function currentMonth(now: Date): string {
  const brazil = new Date(now.getTime() + BRAZIL_OFFSET_HOURS * 3_600_000);
  return `${brazil.getUTCFullYear()}-${pad(brazil.getUTCMonth() + 1)}`;
}

export function addMonths(month: string, delta: number): string {
  const [year, number] = month.split('-').map(Number);
  const index = year * 12 + (number - 1) + delta;
  return `${Math.floor(index / 12)}-${pad((index % 12) + 1)}`;
}

export function isMonth(text: string): boolean {
  const match = MONTH_FORMAT.exec(text);
  return match !== null && Number(match[1]) >= MIN_YEAR && Number(match[1]) <= MAX_YEAR;
}

/** "11/2026", para tabelas. */
export function formatMonth(month: string): string {
  const [year, number] = month.split('-');
  return `${number}/${year}`;
}

/** "Novembro de 2026", para títulos e listas. */
export function monthTitle(month: string): string {
  const text = titleFormatter.format(new Date(`${month}-01T00:00:00Z`));
  return text.charAt(0).toUpperCase() + text.slice(1);
}

/** Quantos meses para trás e para a frente da tela de lançamentos: 5 anos, como a API. */
export const MONTH_WINDOW = 60;

/** Se o mês cabe na janela de 5 anos para trás e para a frente do mês atual. */
export function isMonthInWindow(month: string, now: Date): boolean {
  const current = currentMonth(now);
  return month >= addMonths(current, -MONTH_WINDOW) && month <= addMonths(current, MONTH_WINDOW);
}

/** "2026-11-10" vira "10/11/2026". */
export function formatDate(date: string): string {
  const [year, month, day] = date.split('-');
  return `${day}/${month}/${year}`;
}

/** Os meses de `first` a `last`, inclusive, do mais antigo ao mais novo. */
export function monthOptions(first: string, last: string): MonthOption[] {
  const options: MonthOption[] = [];
  for (let month = first; month <= last; month = addMonths(month, 1)) {
    options.push({ value: month, label: monthTitle(month) });
  }
  return options;
}

function pad(value: number): string {
  return String(value).padStart(2, '0');
}
