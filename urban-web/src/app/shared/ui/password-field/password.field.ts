import { Component, input, signal } from '@angular/core';
import { ReactiveFormsModule } from '@angular/forms';
import { MatIconButton } from '@angular/material/button';
import { MatError, MatFormField, MatHint, MatLabel, MatSuffix } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { PASSWORD_MAX_LENGTH, PASSWORD_MIN_LENGTH } from '../../forms/password.validator';
import { FormFieldBase, USE_PARENT_FORM } from '../form-field.base';

/**
 * Campo de senha das telas de acesso, com o botão de mostrar e ocultar. Liga-se ao controle
 * `password` do formulário em que for colocado.
 */
@Component({
  selector: 'urban-password-field',
  imports: [
    ReactiveFormsModule,
    MatFormField,
    MatLabel,
    MatError,
    MatHint,
    MatSuffix,
    MatInput,
    MatIconButton,
    MatIcon,
  ],
  templateUrl: './password.field.html',
  viewProviders: [USE_PARENT_FORM],
  host: { class: 'block' },
})
export class PasswordField extends FormFieldBase {
  /** `new-password` ao criar ou trocar a senha, para o gerenciador de senhas sugerir uma. */
  readonly autocomplete = input<'current-password' | 'new-password'>('current-password');
  /** Ao criar a senha: mostra a dica da política e o aviso de cada regra. */
  readonly policy = input(false);

  protected readonly controlName = 'password';
  protected readonly minLength = PASSWORD_MIN_LENGTH;
  protected readonly maxLength = PASSWORD_MAX_LENGTH;
  protected readonly visible = signal(false);
}
