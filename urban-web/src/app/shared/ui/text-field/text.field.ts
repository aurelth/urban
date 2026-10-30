import { Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { MaskDirective } from '../../directives/mask.directive';
import { MaskKind } from '../../forms/masks';
import { FormFieldBase, USE_PARENT_FORM } from '../form-field.base';

const FALLBACK_ERROR = 'Confira este campo';

/**
 * Campo de texto dos formulários de cadastro. Liga-se ao controle de nome `name` do formulário (ou
 * do grupo) em que for colocado. O texto de cada erro vem de quem usa: `errors` mapeia a chave do
 * erro do controle para a mensagem, e vale a primeira chave, na ordem do mapa, que o controle tiver.
 */
@Component({
  selector: 'urban-text-field',
  imports: [
    ReactiveFormsModule,
    MatFormField,
    MatLabel,
    MatError,
    MatHint,
    MatInput,
    MaskDirective,
  ],
  templateUrl: './text.field.html',
  viewProviders: [USE_PARENT_FORM],
  // Sem dica, a área do erro não reserva espaço (`subscriptSizing="dynamic"` no template): o espaço de baixo é do campo.
  host: { class: 'block', '[class.mb-5]': '!hint()' },
})
export class TextField extends FormFieldBase {
  readonly name = input.required<string>();
  readonly label = input.required<string>();
  readonly hint = input<string | null>(null);
  readonly mask = input<MaskKind | null>(null);
  readonly autocomplete = input('off');
  /** Teclado do celular nos campos sem máscara (a máscara escolhe o seu). */
  readonly inputmode = input<string | null>(null);
  readonly optional = input(false);
  readonly errors = input<Readonly<Record<string, string>>>({});

  protected get controlName(): string {
    return this.name();
  }

  protected errorMessage(): string {
    const messages = this.errors();
    const key = Object.keys(messages).find((candidate) => this.control?.hasError(candidate));
    return key === undefined ? FALLBACK_ERROR : messages[key];
  }
}
