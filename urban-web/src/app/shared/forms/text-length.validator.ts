import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Tamanho do texto sem os espaços das pontas, como a API o mede. Vazio fica por conta do `requiredText`, para a tela
 * pedir o campo em vez de reclamar do tamanho.
 */
export function trimmedLength(min: number, max: number): ValidatorFn {
  return (control: AbstractControl<string>): ValidationErrors | null => {
    const length = control.value.trim().length;
    if (length === 0) {
      return null;
    }
    if (length < min) {
      return { tooShort: { min } };
    }
    return length > max ? { tooLong: { max } } : null;
  };
}
