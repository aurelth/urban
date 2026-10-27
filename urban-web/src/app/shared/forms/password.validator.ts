import { AbstractControl, ValidationErrors } from '@angular/forms';

export const PASSWORD_MIN_LENGTH = 10;
export const PASSWORD_MAX_LENGTH = 128;

const LETTER = /\p{L}/u;
const DIGIT = /\p{Nd}/u;

/**
 * Espelha a política de senha da API (tamanho de 10 a 128, com letra e número) só para avisar
 * na hora. Quem decide é a API. Vazio fica por conta do Validators.required.
 */
export function passwordPolicy(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value;
  if (value === '') {
    return null;
  }
  if (value.length < PASSWORD_MIN_LENGTH) {
    return { tooShort: true };
  }
  if (value.length > PASSWORD_MAX_LENGTH) {
    return { tooLong: true };
  }
  return LETTER.test(value) && DIGIT.test(value) ? null : { needsLetterAndDigit: true };
}
