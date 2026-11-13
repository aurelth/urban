import { Component, ElementRef, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { RegistrationApi } from '../../../core/auth/registration.api';
import { toApiProblem } from '../../../core/http/api-problem';
import { MessagesByCode, describeProblem } from '../../../core/http/problem-message';
import { displayName } from '../../../shared/forms/display-name.validator';
import { emailFormat } from '../../../shared/forms/email.validator';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { requiredText } from '../../../shared/forms/required-text.validator';
import { EmailField } from '../../../shared/ui/email-field/email.field';
import { FormSubmit } from '../../../shared/ui/form-submit/form-submit';
import { TextField } from '../../../shared/ui/text-field/text.field';

const CONDOMINIUM_NAME_MIN_LENGTH = 2;
const CONDOMINIUM_NAME_MAX_LENGTH = 100;
const CONDOMINIUM_NAME_LENGTH_MESSAGE = `O nome deve ter de ${CONDOMINIUM_NAME_MIN_LENGTH} a ${CONDOMINIUM_NAME_MAX_LENGTH} caracteres`;

const NAME_MESSAGE = 'Use de 2 a 40 letras, sem números';

const REGISTER_MESSAGES: MessagesByCode = {
  'identity.email.invalid': 'Informe um e-mail válido',
  'identity.display_name.invalid': NAME_MESSAGE,
  'identity.requested_condominium_name.invalid': `O nome do condomínio deve ter de ${CONDOMINIUM_NAME_MIN_LENGTH} a ${CONDOMINIUM_NAME_MAX_LENGTH} caracteres`,
};

/**
 * Pedido de cadastro do síndico: e-mail, nome e nome do condomínio. Nada é enviado à pessoa agora; o link de confirmação
 * só sai se o administrador aprovar. A resposta é a mesma para qualquer e-mail, para não revelar quem já tem conta.
 */
@Component({
  selector: 'urban-register-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButton,
    MatCard,
    MatCardContent,
    MatIcon,
    EmailField,
    TextField,
    FormSubmit,
  ],
  templateUrl: './register.page.html',
})
export class RegisterPage {
  private readonly api = inject(RegistrationApi);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, emailFormat]],
    name: ['', [requiredText, displayName]],
    condominiumName: [
      '',
      [
        requiredText,
        Validators.minLength(CONDOMINIUM_NAME_MIN_LENGTH),
        Validators.maxLength(CONDOMINIUM_NAME_MAX_LENGTH),
      ],
    ],
  });
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  /** E-mail do pedido enviado. Preenchido, a tela mostra o aviso no lugar do formulário. */
  protected readonly sentTo = signal<string | null>(null);

  protected readonly nameErrors = {
    required: 'Informe como prefere ser chamado',
    displayName: NAME_MESSAGE,
  };
  protected readonly condominiumNameErrors = {
    required: 'Informe o nome do condomínio',
    minlength: CONDOMINIUM_NAME_LENGTH_MESSAGE,
    maxlength: CONDOMINIUM_NAME_LENGTH_MESSAGE,
  };

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      focusFirstInvalid(this.host.nativeElement, this.form);
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    try {
      const { email, name, condominiumName } = this.form.getRawValue();
      await firstValueFrom(this.api.register(email, name.trim(), condominiumName.trim()));
      this.sentTo.set(email);
    } catch (error) {
      this.error.set(describeProblem(toApiProblem(error), REGISTER_MESSAGES));
    } finally {
      this.submitting.set(false);
    }
  }

  protected editRequest(): void {
    this.sentTo.set(null);
  }
}
