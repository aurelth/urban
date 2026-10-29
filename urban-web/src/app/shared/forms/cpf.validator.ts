import { AbstractControl, ValidationErrors } from '@angular/forms';

const MODULUS = 11;

/**
 * Confere os dígitos verificadores do CPF, com ou sem pontuação, como a API. Vazio fica por conta
 * do Validators.required e quem decide se o CPF vale é a API.
 */
export function cpfNumber(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value;
  return value === '' || isValidCpf(value.replace(/[.\-\s]/g, '')) ? null : { cpf: true };
}

function isValidCpf(digits: string): boolean {
  if (!/^\d{11}$/.test(digits) || new Set(digits).size === 1) {
    return false;
  }
  return (
    checkDigit(digits.slice(0, 9), 10) === Number(digits[9]) &&
    checkDigit(digits.slice(0, 10), 11) === Number(digits[10])
  );
}

function checkDigit(digits: string, firstWeight: number): number {
  let sum = 0;
  for (let index = 0; index < digits.length; index++) {
    sum += Number(digits[index]) * (firstWeight - index);
  }
  const remainder = sum % MODULUS;
  return remainder < 2 ? 0 : MODULUS - remainder;
}
