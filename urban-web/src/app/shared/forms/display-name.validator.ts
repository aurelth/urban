import { AbstractControl, ValidationErrors } from '@angular/forms';

export const DISPLAY_NAME_MAX_LENGTH = 40;
const MIN_LETTERS = 2;
const ALLOWED = /^[\p{L} '.-]*$/u;

/**
 * "Como prefere ser chamado", como a API exige: até 40 caracteres, só letras, espaço, apóstrofo,
 * hífen e ponto, com ao menos 2 letras. Espaços repetidos contam como um só. Vazio fica por conta
 * do Validators.required.
 */
export function displayName(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value;
  return value === '' || isValid(value.split(/\s+/).filter(Boolean).join(' '))
    ? null
    : { displayName: true };
}

function isValid(name: string): boolean {
  return (
    name.length <= DISPLAY_NAME_MAX_LENGTH &&
    ALLOWED.test(name) &&
    (name.match(/\p{L}/gu)?.length ?? 0) >= MIN_LETTERS
  );
}
