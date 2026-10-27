import { AbstractControl, ValidationErrors } from '@angular/forms';

const EMAIL = /^[^\s@]+@[^\s@]+\.[^\s@]+$/;

/**
 * Confere só a forma (algo@dominio.tld). Vazio fica por conta do Validators.required e quem decide
 * se o e-mail vale é a API. Espaços nas pontas já são retirados pelo próprio campo type="email".
 */
export function emailFormat(control: AbstractControl<string>): ValidationErrors | null {
  const value = control.value;
  return value === '' || EMAIL.test(value) ? null : { email: true };
}
