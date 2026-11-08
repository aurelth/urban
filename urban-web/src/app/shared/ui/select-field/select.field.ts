import { Component, computed, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatError, MatFormField, MatHint, MatLabel } from '@angular/material/form-field';
import { FormFieldBase, USE_PARENT_FORM } from '../form-field.base';
import { MenuSelect } from '../menu-select/menu-select';

export interface SelectOption {
  readonly value: string;
  readonly label: string;
}

const FALLBACK_ERROR = 'Confira este campo';

/**
 * Escolha de um valor numa lista suspensa com o visual do menu de situação (`urban-menu-select`). Liga-se ao controle de nome `name` do formulário (ou do grupo) em que for colocada. `emptyLabel` acrescenta uma
 * primeira opção de valor vazio (por exemplo, "Sem data final").
 */
@Component({
  selector: 'urban-select-field',
  imports: [ReactiveFormsModule, MatFormField, MatLabel, MatError, MatHint, MenuSelect],
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

  protected readonly menuOptions = computed<readonly SelectOption[]>(() => {
    const empty = this.emptyLabel();
    return empty === null ? this.options() : [{ value: '', label: empty }, ...this.options()];
  });

  protected get controlName(): string {
    return this.name();
  }

  protected errorMessage(): string {
    const messages = this.errors();
    const key = Object.keys(messages).find((candidate) => this.control?.hasError(candidate));
    return key === undefined ? FALLBACK_ERROR : messages[key];
  }
}
