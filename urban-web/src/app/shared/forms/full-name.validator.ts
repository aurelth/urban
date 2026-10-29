import { AbstractControl, ValidationErrors } from '@angular/forms';

const MAX_LENGTH = 120;
const MIN_EDGE_WORD_LENGTH = 2;
const ALLOWED = /^[\p{L} '.-]+$/u;

/**
 * Nome completo como a API exige: nome e sobrenome, só letras, espaço, apóstrofo, hífen e ponto,
 * até 120 caracteres, com o primeiro e o último nome de ao menos 2 letras. Espaços repetidos
 * contam como um só. Vazio fica por conta do Validators.required.
 */
export function fullName(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value;
  return value === '' || isValidName(value.split(/\s+/).filter(Boolean))
    ? null
    : { fullName: true };
}

function isValidName(words: string[]): boolean {
  const name = words.join(' ');
  return (
    name.length > 0 &&
    name.length <= MAX_LENGTH &&
    ALLOWED.test(name) &&
    words.length >= 2 &&
    words[0].length >= MIN_EDGE_WORD_LENGTH &&
    words[words.length - 1].length >= MIN_EDGE_WORD_LENGTH
  );
}
