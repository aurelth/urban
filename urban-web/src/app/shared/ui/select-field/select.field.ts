import { Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { FormFieldBase, USE_PARENT_FORM } from '../form-field.base';

export interface SelectOption {
  readonly value: string;
  readonly label: string;
}

const FALLBACK_ERROR = 'Confira este campo';

/**
 * Escolha de um valor numa lista. Usa a lista nativa do navegador, que no celular abre o seletor do sistema, e
 * liga-se ao controle de nome `name` do formulário (ou do grupo) em que for colocada. `emptyLabel` acrescenta uma
 * primeira opção de valor vazio (por exemplo, "Sem data final").
 */
@Component({
  selector: 'urban-select-field',
  imports: [ReactiveFormsModule, MatFormField, MatLabel, MatError, MatHint, MatInput],
  templateUrl: './select.field.html',
  viewProviders: [USE_PARENT_FORM],
  host: { class: 'block', '[class.mb-5]': '!hint()' },
})
export class SelectField extends FormFieldBase {
  readonly name = input.required<string>();
  readonly label = input.required<string>();
  readonly options = input.required<readonly SelectOption[]>();
  readonly emptyLabel = input<string | null>(null);
  readonly hint = input<string | null>(null);
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
