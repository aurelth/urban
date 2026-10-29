import { Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { ADDRESS_MAX_LENGTH } from '../../forms/address.group';
import { USE_PARENT_FORM } from '../form-field.base';
import { StateField } from '../state-field/state.field';
import { TextField } from '../text-field/text.field';

/**
 * Os sete campos de um endereço, dentro do grupo `groupName` do formulário em que for colocado
 * (veja `addressGroup`). `personal` diz se é o endereço da própria pessoa, caso em que o navegador
 * pode sugerir o endereço salvo; num endereço de outro lugar, as sugestões ficam desligadas.
 */
@Component({
  selector: 'urban-address-fields',
  imports: [ReactiveFormsModule, TextField, StateField],
  templateUrl: './address.fields.html',
  viewProviders: [USE_PARENT_FORM],
})
export class AddressFields {
  readonly groupName = input.required<string>();
  readonly personal = input(true);

  protected readonly postalCodeErrors = {
    required: 'Informe o CEP',
    postalCode: 'Informe um CEP válido',
  };
  protected readonly streetErrors = {
    required: 'Informe a rua',
    maxlength: `A rua deve ter até ${ADDRESS_MAX_LENGTH.street} caracteres`,
  };
  protected readonly numberErrors = {
    required: 'Informe o número (use S/N quando não houver)',
    maxlength: `O número deve ter até ${ADDRESS_MAX_LENGTH.number} caracteres`,
  };
  protected readonly complementErrors = {
    maxlength: `O complemento deve ter até ${ADDRESS_MAX_LENGTH.complement} caracteres`,
  };
  protected readonly neighborhoodErrors = {
    required: 'Informe o bairro',
    maxlength: `O bairro deve ter até ${ADDRESS_MAX_LENGTH.neighborhood} caracteres`,
  };
  protected readonly cityErrors = {
    required: 'Informe a cidade',
    maxlength: `A cidade deve ter até ${ADDRESS_MAX_LENGTH.city} caracteres`,
  };

  protected suggest(token: string): string {
    return this.personal() ? token : 'off';
  }
}
