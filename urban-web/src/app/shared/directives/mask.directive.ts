import { Directive, ElementRef, forwardRef, inject, input } from '@angular/core';
import { ControlValueAccessor, NG_VALUE_ACCESSOR } from '@angular/forms';
import { MASK_INPUTMODE, MaskKind, applyMask, caretAfter, countSignificant } from '../forms/masks';

/**
 * Formata o campo enquanto a pessoa digita (CPF, CNPJ, CEP, telefone e data), sem biblioteca.
 * O controle guarda o texto como aparece; só dígitos vão para a API, via `unmask`. Sendo o
 * próprio ControlValueAccessor do campo, não depende da ordem em que dois ouvintes de `input`
 * rodariam.
 */
@Directive({
  selector: 'input[urbanMask]',
  providers: [
    { provide: NG_VALUE_ACCESSOR, useExisting: forwardRef(() => MaskDirective), multi: true },
  ],
  host: {
    '[attr.inputmode]': 'inputmode()',
    '(input)': 'onInput()',
    '(blur)': 'onTouched()',
  },
})
export class MaskDirective implements ControlValueAccessor {
  readonly urbanMask = input.required<MaskKind>();

  private readonly element = inject<ElementRef<HTMLInputElement>>(ElementRef).nativeElement;
  private onChange: (value: string) => void = () => undefined;
  protected onTouched: () => void = () => undefined;

  protected inputmode(): 'numeric' | 'text' | 'tel' {
    return MASK_INPUTMODE[this.urbanMask()];
  }

  writeValue(value: string | null): void {
    this.element.value = applyMask(this.urbanMask(), value ?? '');
  }

  registerOnChange(onChange: (value: string) => void): void {
    this.onChange = onChange;
  }

  registerOnTouched(onTouched: () => void): void {
    this.onTouched = onTouched;
  }

  setDisabledState(disabled: boolean): void {
    this.element.disabled = disabled;
  }

  protected onInput(): void {
    const kind = this.urbanMask();
    const typed = this.element.value;
    // Só campos de texto têm cursor que dá para ler e mover (o de e-mail, por exemplo, não tem).
    const caret = this.element.selectionStart;
    const significantBefore = caret === null ? 0 : countSignificant(kind, typed.slice(0, caret));
    const masked = applyMask(kind, typed);

    this.element.value = masked;
    if (caret !== null) {
      const position = caretAfter(kind, masked, significantBefore);
      this.element.setSelectionRange(position, position);
    }
    this.onChange(masked);
  }
}
