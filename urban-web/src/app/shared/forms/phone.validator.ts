import { AbstractControl, ValidationErrors } from '@angular/forms';

const COUNTRY_CODE = '55';

/** DDDs que existem no Brasil, os mesmos que a API aceita, agrupados pelo primeiro dígito. */
const AREA_CODES: ReadonlySet<string> = new Set(
  [
    '11 12 13 14 15 16 17 18 19',
    '21 22 24 27 28',
    '31 32 33 34 35 37 38',
    '41 42 43 44 45 46 47 48 49',
    '51 53 54 55',
    '61 62 63 64 65 66 67 68 69',
    '71 73 74 75 77 79',
    '81 82 83 84 85 86 87 88 89',
    '91 92 93 94 95 96 97 98 99',
  ]
    .join(' ')
    .split(' '),
);

/**
 * Telefone brasileiro com DDD: fixo (10 dígitos, começando em 2 a 5 depois do DDD) ou celular
 * (11 dígitos, com 9 depois do DDD). O +55 é aceito e descartado, como na API. Vazio fica por
 * conta do Validators.required.
 */
export function brazilianPhone(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value;
  return value === '' || isValidPhone(withoutCountryCode(value.replace(/[()\-.+\s]/g, '')))
    ? null
    : { phone: true };
}

function withoutCountryCode(digits: string): string {
  return (digits.length === 12 || digits.length === 13) && digits.startsWith(COUNTRY_CODE)
    ? digits.slice(2)
    : digits;
}

function isValidPhone(digits: string): boolean {
  if (!/^\d{10,11}$/.test(digits) || !AREA_CODES.has(digits.slice(0, 2))) {
    return false;
  }
  return digits.length === 11 ? digits[2] === '9' : /[2-5]/.test(digits[2]);
}
