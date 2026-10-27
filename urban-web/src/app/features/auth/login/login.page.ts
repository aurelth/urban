import { Component, ElementRef, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatError, MatFormField, MatLabel, MatSuffix } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { Router, RouterLink } from '@angular/router';
import { toApiProblem } from '../../../core/http/api-problem';
import { MessagesByCode, describeProblem } from '../../../core/http/problem-message';
import { safeReturnUrl } from '../../../core/auth/return-url';
import { SessionStore } from '../../../core/auth/session.store';
import { emailFormat } from '../../../shared/forms/email.validator';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';

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
    MatButton,
    MatIconButton,
    MatCard,
    MatCardContent,
    MatFormField,
    MatLabel,
    MatError,
    MatSuffix,
    MatInput,
    MatIcon,
    RouterLink,
  ],
  templateUrl: './login.page.html',
})
export class LoginPage {
  /** Destino guardado pela guarda de rota (?returnUrl=...). */
  readonly returnUrl = input<string>();

  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, emailFormat]],
    password: ['', Validators.required],
  });
  protected readonly showPassword = signal(false);
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
