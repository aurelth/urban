import { FormGroup } from '@angular/forms';

/** Leva o foco ao primeiro campo inválido, inclusive o que fica dentro de um componente (checkbox). */
export function focusFirstInvalid(host: HTMLElement, form: FormGroup): void {
  const name = Object.keys(form.controls).find((key) => form.controls[key].invalid);
  const element = host.querySelector<HTMLElement>(`[formControlName="${name}"]`);
  const target = element?.matches('input') ? element : element?.querySelector('input');
  target?.focus();
}
