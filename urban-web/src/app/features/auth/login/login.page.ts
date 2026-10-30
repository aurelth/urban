import { Component, ElementRef, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { Router, RouterLink } from '@angular/router';
import { toApiProblem } from '../../../core/http/api-problem';
import { MessagesByCode, describeProblem } from '../../../core/http/problem-message';
import { safeReturnUrl } from '../../../core/auth/return-url';
import { SessionStore } from '../../../core/auth/session.store';
import { emailFormat } from '../../../shared/forms/email.validator';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { EmailField } from '../../../shared/ui/email-field/email.field';
import { FormSubmit } from '../../../shared/ui/form-submit/form-submit';
import { PasswordField } from '../../../shared/ui/password-field/password.field';

const LOGIN_MESSAGES: MessagesByCode = {
  'identity.login.invalid_credentials': 'E-mail ou senha incorretos',
  'identity.login.locked_out':
    'Conta bloqueada por muitas tentativas. Tente novamente em 15 minutos',
  'identity.user.email_not_verified': 'Confirme seu e-mail pelo link que enviamos antes de entrar',
  'identity.user.account_deactivated': 'Conta desativada. Fale com o síndico',
  'identity.user.account_suspended': 'Acesso suspenso. Fale com o suporte',
  'identity.user.account_deleted': 'Conta excluída',
};

@Component({
  selector: 'urban-login-page',
  imports: [
    ReactiveFormsModule,
    MatCard,
    MatCardContent,
    MatIcon,
    RouterLink,
    EmailField,
    PasswordField,
    FormSubmit,
  ],
  templateUrl: './login.page.html',
})
export class LoginPage {
  /** Destino guardado pela guarda de rota (?returnUrl=...). */
  readonly returnUrl = input<string>();

  /** Por que a pessoa chegou aqui sem ter clicado em sair (?motivo=inatividade ou ?motivo=senha-alterada). */
  readonly motivo = input<string>();

  protected readonly leftByInactivity = computed(() => this.motivo() === 'inatividade');
  protected readonly passwordChanged = computed(() => this.motivo() === 'senha-alterada');

  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, emailFormat]],
    password: ['', Validators.required],
  });
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      focusFirstInvalid(this.host.nativeElement, this.form);
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    try {
      const { email, password } = this.form.getRawValue();
      await this.session.signIn(email, password);
      await this.router.navigateByUrl(safeReturnUrl(this.returnUrl()));
    } catch (error) {
      this.error.set(describeProblem(toApiProblem(error), LOGIN_MESSAGES));
    } finally {
      this.submitting.set(false);
    }
  }
}
