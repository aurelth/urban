import { Component, ElementRef, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAnchor } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { PasswordResetApi } from '../../../core/auth/password-reset.api';
import { SessionStore } from '../../../core/auth/session.store';
import { toApiProblem } from '../../../core/http/api-problem';
import { MessagesByCode, describeProblem } from '../../../core/http/problem-message';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { passwordPolicy } from '../../../shared/forms/password.validator';
import { FormSubmit } from '../../../shared/ui/form-submit/form-submit';
import { PasswordField } from '../../../shared/ui/password-field/password.field';
import { LinkProblemNotice } from '../link-problem/link-problem.notice';
import { PASSWORD_POLICY_MESSAGES } from '../password-messages';

const RESET_MESSAGES: MessagesByCode = {
  'identity.password_reset.invalid': 'Link inválido ou já usado. Peça um novo link',
  'identity.password_reset.expired': 'Link expirado. Peça um novo link',
  ...PASSWORD_POLICY_MESSAGES,
};

const LINK_ERROR_CODES: ReadonlySet<string> = new Set([
  'identity.password_reset.invalid',
  'identity.password_reset.expired',
]);

const MISSING_TOKEN_MESSAGE =
  'Este link está incompleto. Abra o link do e-mail de novo ou peça um novo link';

@Component({
  selector: 'urban-reset-password-page',
  imports: [
    ReactiveFormsModule,
    MatAnchor,
    MatCard,
    MatCardContent,
    MatIcon,
    RouterLink,
    PasswordField,
    FormSubmit,
    LinkProblemNotice,
  ],
  templateUrl: './reset-password.page.html',
})
export class ResetPasswordPage {
  /** Token do link do e-mail (?token=...). */
  readonly token = input<string>();

  private readonly api = inject(PasswordResetApi);
  private readonly session = inject(SessionStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly form = inject(NonNullableFormBuilder).group({
    password: ['', [Validators.required, passwordPolicy]],
  });
  protected readonly submitting = signal(false);
  protected readonly done = signal(false);
  protected readonly error = signal<string | null>(null);
  /** Mensagem do link que não serve mais (sem token, já usado ou vencido). */
  protected readonly linkError = signal<string | null>(null);
  protected readonly blockedMessage = computed(
    () => this.linkError() ?? (this.token() ? null : MISSING_TOKEN_MESSAGE),
  );

  protected async submit(): Promise<void> {
    const token = this.token();
    if (this.form.invalid || !token) {
      focusFirstInvalid(this.host.nativeElement, this.form);
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    try {
      await firstValueFrom(this.api.reset(token, this.form.getRawValue().password));
      // A API revoga todas as sessões da conta; este navegador deixa de se achar logado.
      this.session.expire();
      this.done.set(true);
    } catch (error) {
      const problem = toApiProblem(error);
      const message = describeProblem(problem, RESET_MESSAGES);
      if (problem.code !== null && LINK_ERROR_CODES.has(problem.code)) {
        this.linkError.set(message);
      } else {
        this.error.set(message);
      }
    } finally {
      this.submitting.set(false);
    }
  }
}
