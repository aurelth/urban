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

/** A API só abre o mês atual, os anteriores e o seguinte. */
export function canOpenMonth(month: string, now: Date): boolean {
  return month <= addMonths(currentMonth(now), 1);
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
