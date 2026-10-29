import { AbstractControl, ValidationErrors } from '@angular/forms';

/**
 * Confere só a forma do CEP (8 dígitos, não todos zero). Se o CEP existe, quem sabe é o Correio.
 * Vazio fica por conta do Validators.required.
 */
export function postalCodeFormat(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value;
  if (value === '') {
    return null;
  }
  const digits = value.replace(/[.\-\s]/g, '');
  return /^\d{8}$/.test(digits) && /[1-9]/.test(digits) ? null : { postalCode: true };
}
