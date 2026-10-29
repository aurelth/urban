import { AbstractControl, ValidationErrors } from '@angular/forms';

/** Como Validators.required, mas texto só de espaços também conta como não preenchido, como na API. */
export function requiredText(control: AbstractControl<string>): ValidationErrors | null {
  return control.value.trim() === '' ? { required: true } : null;
}
