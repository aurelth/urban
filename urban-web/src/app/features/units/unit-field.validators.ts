import { AbstractControl, ValidationErrors } from '@angular/forms';
import { parseArea, parsePercent } from './unit-values';

/** Fração ideal opcional, digitada em porcentagem. Vazio passa; o resto precisa ser um valor que a API aceita. */
export function percentField(control: AbstractControl<string>): ValidationErrors | null {
  return parsePercent(control.value).valid ? null : { percent: true };
}

/** Área opcional, digitada em m². Vazio passa; o resto precisa ser um valor que a API aceita. */
export function areaField(control: AbstractControl<string>): ValidationErrors | null {
  return parseArea(control.value).valid ? null : { area: true };
}
