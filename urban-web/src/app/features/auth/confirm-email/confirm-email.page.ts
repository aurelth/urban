import { Component, ElementRef, OnInit, computed, inject, input, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatAnchor, MatButton } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatCheckbox } from '@angular/material/checkbox';
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { RegistrationApi } from '../../../core/auth/registration.api';
import { ApiProblem, toApiProblem } from '../../../core/http/api-problem';
import { MessagesByCode, describeProblem } from '../../../core/http/problem-message';
import { focusFirstInvalid } from '../../../shared/forms/focus-first-invalid';
import { passwordPolicy } from '../../../shared/forms/password.validator';
import { PasswordField } from '../../../shared/ui/password-field/password.field';
import { LinkProblemNotice } from '../link-problem/link-problem.notice';
import { PASSWORD_POLICY_MESSAGES } from '../password-messages';

const CONFIRM_MESSAGES: MessagesByCode = {
  'identity.email_verification.invalid': 'Link inválido ou já usado. Faça o cadastro novamente',
  'identity.email_verification.expired':
    'Link expirado. Faça o cadastro novamente para receber outro',
  ...PASSWORD_POLICY_MESSAGES,
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
    MatCard,
    MatCardContent,
    MatCheckbox,
    MatIcon,
    RouterLink,
    PasswordField,
    LinkProblemNotice,
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
