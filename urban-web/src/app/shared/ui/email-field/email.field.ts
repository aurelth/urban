import { Component, input } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatError, MatFormField, MatLabel } from '@angular/material/form-field';
import { MatInput } from '@angular/material/input';
import { FormFieldBase, USE_PARENT_FORM } from '../form-field.base';

/**
 * Campo de e-mail das telas de acesso. Liga-se ao controle `email` do formulário em que for
 * colocado, então a tela só escreve `<urban-email-field />` dentro do seu `<form [formGroup]>`.
 */
@Component({
  selector: 'urban-email-field',
  imports: [ReactiveFormsModule, MatFormField, MatLabel, MatError, MatInput],
  templateUrl: './email.field.html',
  viewProviders: [USE_PARENT_FORM],
  host: { class: 'block' },
})
export class EmailField extends FormFieldBase {
  /** `username` na entrada, para o gerenciador de senhas oferecer a conta salva. */
  readonly autocomplete = input<'email' | 'username'>('email');

  protected readonly controlName = 'email';
}
