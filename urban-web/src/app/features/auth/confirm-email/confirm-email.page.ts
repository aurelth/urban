import { Component, ElementRef, OnInit, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAnchor, MatButton, MatIconButton } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatCheckbox } from '@angular/material/checkbox';
import { MatError, MatFormField, MatHint, MatLabel, MatSuffix } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { RegistrationApi } from '../../../core/auth/registration.api';
import { ApiProblem, toApiProblem } from '../../../core/http/api-problem';
import { MessagesByCode, describeProblem } from '../../../core/http/problem-message';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import {
  PASSWORD_MAX_LENGTH,
  PASSWORD_MIN_LENGTH,
  passwordPolicy,
} from '../../../shared/forms/password.validator';

const CONFIRM_MESSAGES: MessagesByCode = {
  'identity.email_verification.invalid': 'Link inválido ou já usado. Faça o cadastro novamente',
  'identity.email_verification.expired':
    'Link expirado. Faça o cadastro novamente para receber outro',
  'identity.password.too_short': `A senha precisa ter pelo menos ${PASSWORD_MIN_LENGTH} caracteres`,
  'identity.password.too_long': `A senha pode ter no máximo ${PASSWORD_MAX_LENGTH} caracteres`,
  'identity.password.needs_letter_and_digit':
    'A senha precisa ter pelo menos uma letra e um número',
  'identity.consent.required': 'Aceite os termos de uso e a política de privacidade para continuar',
  'identity.consent.outdated': 'Os termos foram atualizados. Leia e aceite de novo para continuar',
};

const LINK_ERROR_CODES: ReadonlySet<string> = new Set([
  'identity.email_verification.invalid',
  'identity.email_verification.expired',
]);

const MISSING_TOKEN_MESSAGE =
  'Este link está incompleto. Abra o link do e-mail de novo ou faça o cadastro novamente';

@Component({
  selector: 'urban-confirm-email-page',
  imports: [
    ReactiveFormsModule,
    MatAnchor,
    MatButton,
    MatIconButton,
    MatCard,
    MatCardContent,
    MatCheckbox,
    MatFormField,
    MatLabel,
    MatError,
    MatHint,
    MatSuffix,
    MatInput,
    MatIcon,
    RouterLink,
  ],
  templateUrl: './confirm-email.page.html',
})
export class ConfirmEmailPage implements OnInit {
  /** Token do link do e-mail (?token=...). */
  readonly token = input<string>();

  private readonly api = inject(RegistrationApi);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);

  protected readonly form = inject(NonNullableFormBuilder).group({
    password: ['', [Validators.required, passwordPolicy]],
    accepted: [false, Validators.requiredTrue],
  });
  protected readonly passwordMinLength = PASSWORD_MIN_LENGTH;
  protected readonly passwordMaxLength = PASSWORD_MAX_LENGTH;
  protected readonly showPassword = signal(false);
  protected readonly submitting = signal(false);
  protected readonly attempted = signal(false);
  protected readonly done = signal(false);
  protected readonly error = signal<string | null>(null);
  /** Mensagem do link que não serve mais (sem token, já usado ou vencido). */
  protected readonly linkError = signal<string | null>(null);
  protected readonly termsVersion = signal<string | null>(null);
  protected readonly termsFailed = signal(false);
  protected readonly blockedMessage = computed(
    () => this.linkError() ?? (this.token() ? null : MISSING_TOKEN_MESSAGE),
  );

  ngOnInit(): void {
    if (this.token()) {
      void this.loadTerms();
    }
  }

  protected async loadTerms(): Promise<void> {
    this.termsFailed.set(false);
    try {
      this.termsVersion.set((await firstValueFrom(this.api.currentTerms())).version);
    } catch {
      this.termsFailed.set(true);
    }
  }

  protected async submit(): Promise<void> {
    const token = this.token();
    const version = this.termsVersion();
    if (this.form.invalid || !token || version === null) {
      this.attempted.set(true);
      focusFirstInvalid(this.host.nativeElement, this.form);
      return;
    }
    this.submitting.set(true);
    this.error.set(null);
    try {
      const { password } = this.form.getRawValue();
      await firstValueFrom(this.api.confirmEmail(token, password, version));
      this.done.set(true);
    } catch (error) {
      await this.showFailure(toApiProblem(error));
    } finally {
      this.submitting.set(false);
    }
  }

  private async showFailure(problem: ApiProblem): Promise<void> {
    const message = describeProblem(problem, CONFIRM_MESSAGES);
    if (problem.code !== null && LINK_ERROR_CODES.has(problem.code)) {
      this.linkError.set(message);
      return;
    }
    this.error.set(message);
    if (problem.code === 'identity.consent.outdated') {
      this.form.controls.accepted.setValue(false);
      await this.loadTerms();
    }
  }
}
