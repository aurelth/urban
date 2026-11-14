import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Formato antigo (ABC1234) ou Mercosul (ABC1D23), sem hífen nem espaço. */
const PLATE = /^[A-Z]{3}\d(\d{3}|[A-Z]\d{2})$/;

/**
 * Confere só a forma da placa, como a API (`LicensePlate.Normalize`): hífen, espaços e caixa não contam. Vazio fica por conta do
 * `requiredText`.
 */
export function licensePlate(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value;
  if (value === '') {
    return null;
  }
  return PLATE.test(value.replace(/[\s-]/g, '').toUpperCase()) ? null : { licensePlate: true };
}
