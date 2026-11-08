import { Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { BRAZILIAN_STATES } from '../../models/brazilian-states';
import { FormFieldBase, USE_PARENT_FORM } from '../form-field.base';
import { MenuOption, MenuSelect } from '../menu-select/menu-select';

/**
 * Escolha da UF numa lista suspensa com o visual do menu de situação (`urban-menu-select`). Liga-se
 * ao controle de nome `name` do formulário (ou do grupo) em que for colocada.
 */
@Component({
  selector: 'urban-state-field',
  imports: [ReactiveFormsModule, MatFormField, MatLabel, MatError, MenuSelect],
  templateUrl: './state.field.html',
  viewProviders: [USE_PARENT_FORM],
  host: { class: 'block mb-5' },
})
export class StateField extends FormFieldBase {
  readonly name = input.required<string>();

  protected readonly states: readonly MenuOption[] = BRAZILIAN_STATES.map((state) => ({
    value: state,
    label: state,
  }));

  protected get controlName(): string {
    return this.name();
  }
}
