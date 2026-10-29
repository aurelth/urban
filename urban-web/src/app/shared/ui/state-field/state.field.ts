import { Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { BRAZILIAN_STATES } from '../../models/brazilian-states';
import { FormFieldBase, USE_PARENT_FORM } from '../form-field.base';

/**
 * Escolha da UF. Usa a lista nativa do navegador, que no celular abre o seletor do sistema, e liga-se
 * ao controle de nome `name` do formulário (ou do grupo) em que for colocada.
 */
@Component({
  selector: 'urban-state-field',
  imports: [ReactiveFormsModule, MatFormField, MatLabel, MatError, MatInput],
  templateUrl: './state.field.html',
  viewProviders: [USE_PARENT_FORM],
  host: { class: 'block' },
})
export class StateField extends FormFieldBase {
  readonly name = input.required<string>();
  readonly autocomplete = input('off');

  protected readonly states = BRAZILIAN_STATES;

  protected get controlName(): string {
    return this.name();
  }
}
