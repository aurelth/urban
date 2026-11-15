import { AbstractControl, ValidationErrors } from '@angular/forms';
import { parseArea, parseParkingSpots, parsePercent } from './unit-values';

/** Fração ideal opcional, digitada em porcentagem. Vazio passa; o resto precisa ser um valor que a API aceita. */
export function percentField(control: AbstractControl<string>): ValidationErrors | null {
  return parsePercent(control.value).valid ? null : { percent: true };
}

/** Área opcional, digitada em m². Vazio passa; o resto precisa ser um valor que a API aceita. */
export function areaField(control: AbstractControl<string>): ValidationErrors | null {
  return parseArea(control.value).valid ? null : { area: true };
}

const PARKING_SPOT_ERRORS = {
  tooMany: { parkingSpotsTooMany: true },
  tooLong: { parkingSpotsTooLong: true },
  duplicate: { parkingSpotsDuplicate: true },
} as const;

/** Vagas de garagem opcionais, separadas por vírgula. Vazio passa; o resto precisa respeitar o que a API aceita. */
export function parkingSpotsField(control: AbstractControl<string>): ValidationErrors | null {
  const spots = parseParkingSpots(control.value);
  return spots.valid ? null : PARKING_SPOT_ERRORS[spots.reason];
}
