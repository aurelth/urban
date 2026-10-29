import { AbstractControl, ValidationErrors } from '@angular/forms';

const FIRST_WEIGHTS = [5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
const SECOND_WEIGHTS = [6, 5, 4, 3, 2, 9, 8, 7, 6, 5, 4, 3, 2];
const ASCII_ZERO = 48;
const MODULUS = 11;

/**
 * Confere os dígitos verificadores do CNPJ numérico ou alfanumérico, com ou sem pontuação, como a
 * API. Vazio fica por conta do Validators.required e quem decide se o CNPJ vale é a API.
 */
export function cnpjNumber(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value;
  return value === '' || isValidCnpj(value.replace(/[./\-\s]/g, '').toUpperCase())
    ? null
    : { cnpj: true };
}

function isValidCnpj(value: string): boolean {
  if (!/^[0-9A-Z]{14}$/.test(value) || new Set(value).size === 1) {
    return false;
  }
  return (
    checkDigit(value.slice(0, 12), FIRST_WEIGHTS) === digitAt(value, 12) &&
    checkDigit(value.slice(0, 13), SECOND_WEIGHTS) === digitAt(value, 13)
  );
}

/** O valor de cada caractere é o código ASCII menos 48: dígitos valem 0 a 9 e letras, 17 a 42. */
function charValue(character: string): number {
  return character.charCodeAt(0) - ASCII_ZERO;
}

/** Uma letra no lugar do verificador vale 17 ou mais e nunca coincide com o dígito calculado. */
function digitAt(value: string, index: number): number {
  return charValue(value[index]);
}

function checkDigit(characters: string, weights: number[]): number {
  let sum = 0;
  for (let index = 0; index < characters.length; index++) {
    sum += charValue(characters[index]) * weights[index];
  }
  const remainder = sum % MODULUS;
  return remainder < 2 ? 0 : MODULUS - remainder;
}
