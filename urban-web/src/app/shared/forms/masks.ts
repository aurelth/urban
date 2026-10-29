export type MaskKind = 'cpf' | 'cnpj' | 'cep' | 'phone' | 'date';

/** Teclado que o celular abre em cada campo. O CNPJ pode ter letras, então usa o teclado de texto. */
export const MASK_INPUTMODE: Record<MaskKind, 'numeric' | 'text' | 'tel'> = {
  cpf: 'numeric',
  cnpj: 'text',
  cep: 'numeric',
  phone: 'tel',
  date: 'numeric',
};

const CPF_LENGTH = 11;
const CNPJ_LENGTH = 14;
const CNPJ_BASE_LENGTH = 12;
const CEP_LENGTH = 8;
const DATE_LENGTH = 8;
const PHONE_MAX_LENGTH = 11;
const PHONE_LANDLINE_LENGTH = 10;
const COUNTRY_CODE = '55';

/**
 * Cada máscara é um desenho em que `#` é um caractere do valor e o resto é enfeite. O enfeite só
 * aparece quando ainda há valor depois dele, então ninguém vê um ponto ou barra sobrando no fim.
 */
const CPF_PATTERN = '###.###.###-##';
const CNPJ_PATTERN = '##.###.###/####-##';
const CEP_PATTERN = '#####-###';
const DATE_PATTERN = '##/##/####';
const LANDLINE_PATTERN = '(##) ####-####';
const MOBILE_PATTERN = '(##) #####-####';

export function applyMask(kind: MaskKind, value: string): string {
  switch (kind) {
    case 'cpf':
      return draw(CPF_PATTERN, digits(value, CPF_LENGTH));
    case 'cnpj':
      return draw(CNPJ_PATTERN, cnpjCharacters(value));
    case 'cep':
      return draw(CEP_PATTERN, digits(value, CEP_LENGTH));
    case 'date':
      return draw(DATE_PATTERN, digits(value, DATE_LENGTH));
    case 'phone': {
      const number = phoneDigits(value);
      return draw(
        number.length > PHONE_LANDLINE_LENGTH ? MOBILE_PATTERN : LANDLINE_PATTERN,
        number,
      );
    }
  }
}

/**
 * O que vai para a API: só dígitos (CPF, CEP e telefone), o CNPJ em maiúsculas sem pontuação e a
 * data em aaaa-mm-dd. Data incompleta ou que não existe vira texto vazio.
 */
export function unmask(kind: MaskKind, value: string): string {
  switch (kind) {
    case 'cpf':
      return digits(value, CPF_LENGTH);
    case 'cnpj':
      return cnpjCharacters(value);
    case 'cep':
      return digits(value, CEP_LENGTH);
    case 'phone':
      return phoneDigits(value);
    case 'date':
      return toIsoDate(value);
  }
}

/** Quantos caracteres do valor (e não da pontuação) há em `text`. Serve para achar o cursor. */
export function countSignificant(kind: MaskKind, text: string): number {
  const pattern = kind === 'cnpj' ? /[0-9a-z]/gi : /[0-9]/g;
  return text.match(pattern)?.length ?? 0;
}

/** Posição do cursor logo depois do `significant`-ésimo caractere do valor em `masked`. */
export function caretAfter(kind: MaskKind, masked: string, significant: number): number {
  if (significant <= 0) {
    return 0;
  }
  let seen = 0;
  for (let index = 0; index < masked.length; index++) {
    seen += countSignificant(kind, masked[index]);
    if (seen === significant) {
      return index + 1;
    }
  }
  return masked.length;
}

function draw(pattern: string, characters: string): string {
  let result = '';
  let next = 0;
  for (const slot of pattern) {
    if (next === characters.length) {
      break;
    }
    if (slot === '#') {
      result += characters[next++];
    } else {
      result += slot;
    }
  }
  return result;
}

function digits(value: string, maxLength: number): string {
  return value.replace(/\D/g, '').slice(0, maxLength);
}

/** Os 12 primeiros caracteres do CNPJ podem ser letras ou dígitos; os dois últimos, os verificadores, só dígitos. */
function cnpjCharacters(value: string): string {
  const characters = value.replace(/[^0-9a-zA-Z]/g, '').toUpperCase();
  const base = characters.slice(0, CNPJ_BASE_LENGTH);
  const verifiers = characters.slice(CNPJ_BASE_LENGTH).replace(/\D/g, '');
  return (base + verifiers).slice(0, CNPJ_LENGTH);
}

/** O +55 só é descartado quando vem com o sinal de mais, para não confundir com o DDD 55. */
function phoneDigits(value: string): string {
  const all = value.replace(/\D/g, '');
  const number =
    value.trimStart().startsWith('+') && all.startsWith(COUNTRY_CODE) ? all.slice(2) : all;
  return number.slice(0, PHONE_MAX_LENGTH);
}

function toIsoDate(text: string): string {
  const match = /^(\d{2})\/(\d{2})\/(\d{4})$/.exec(text);
  if (!match) {
    return '';
  }
  const [, day, month, year] = match;
  return isRealDate(Number(year), Number(month), Number(day)) ? `${year}-${month}-${day}` : '';
}

/** Uma data com mês ou dia fora do calendário "escorrega" para outro ano ou dia; se nada escorregou, ela existe. */
export function isRealDate(year: number, month: number, day: number): boolean {
  const date = new Date(0);
  date.setUTCFullYear(year, month - 1, day);
  return date.getUTCFullYear() === year && date.getUTCDate() === day;
}
