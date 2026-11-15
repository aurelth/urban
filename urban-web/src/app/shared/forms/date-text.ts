import { isRealDate } from './masks';

const DATE_TEXT = /^(\d{2})\/(\d{2})\/(\d{4})$/;

/** Lê `dd/mm/aaaa` como data local à meia-noite; texto incompleto ou data que não existe vira `null`. */
export function dateFromText(text: string): Date | null {
  const match = DATE_TEXT.exec(text);
  if (match === null) {
    return null;
  }
  const [day, month, year] = [Number(match[1]), Number(match[2]), Number(match[3])];
  if (!isRealDate(year, month, day)) {
    return null;
  }
  const date = new Date(2000, 0, 1);
  // O construtor trataria ano de 0 a 99 como 19xx; `setFullYear` não.
  date.setFullYear(year, month - 1, day);
  return date;
}

export function textFromDate(date: Date): string {
  const day = String(date.getDate()).padStart(2, '0');
  const month = String(date.getMonth() + 1).padStart(2, '0');
  const year = String(date.getFullYear()).padStart(4, '0');
  return `${day}/${month}/${year}`;
}
