import { AbstractControl, ValidationErrors, ValidatorFn } from '@angular/forms';

/**
 * Quem tem menos de 18 anos é dispensado de profissão e de contato de emergência. Com o campo em branco passa; preenchido,
 * vale o validador de sempre (a API faz a mesma conferência). `isMinor` é lido a cada validação, não ao montar o formulário.
 */
export function unlessMinor(isMinor: () => boolean, validator: ValidatorFn): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const blank = String(control.value ?? '').trim() === '';
    return isMinor() && blank ? null : validator(control);
  };
}

/** O menor só pode ter o vínculo de dependente (`minorKind`). Vínculo ainda não escolhido fica por conta do `required`. */
export function minorKind(isMinor: () => boolean): ValidatorFn {
  return (control: AbstractControl<string>): ValidationErrors | null => {
    const kind = control.value;
    return isMinor() && kind !== '' && kind !== 'dependent' ? { minorKind: true } : null;
  };
}
