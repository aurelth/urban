import { Component, ElementRef, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAnchor, MatButton } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { PasswordResetApi } from '../../../core/auth/password-reset.api';
import { toApiProblem } from '../../../core/http/api-problem';
import { MessagesByCode, describeProblem } from '../../../core/http/problem-message';
import { emailFormat } from '../../../shared/forms/email.validator';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { EmailField } from '../../../shared/ui/email-field/email.field';

const FORGOT_MESSAGES: MessagesByCode = {
  'identity.email.invalid': 'Informe um e-mail válido',
};

@Component({
  selector: 'urban-forgot-password-page',
  imports: [
    ReactiveFormsModule,
    MatAnchor,
    MatButton,
    MatCard,
    MatCardContent,
    MatIcon,
    RouterLink,
    EmailField,
  ],
  templateUrl: './forgot-password.page.html',
})
export class ForgotPasswordPage {
  private readonly api = inject(PasswordResetApi);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, emailFormat]],
  });
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  /** E-mail para onde o link foi pedido. Preenchido, a tela mostra o aviso no lugar do formulário. */
  protected readonly sentTo = signal<string | null>(null);

  protected async submit(): Promise<void> {
    if (this.form.invalid) {
      focusFirstInvalid(this.host.nativeElement, this.form);
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    try {
      const { email } = this.form.getRawValue();
      await firstValueFrom(this.api.requestLink(email));
      this.sentTo.set(email);
    } catch (error) {
      this.error.set(describeProblem(toApiProblem(error), FORGOT_MESSAGES));
    } finally {
      this.submitting.set(false);
    }
  }

  protected changeEmail(): void {
    this.sentTo.set(null);
  }
}
