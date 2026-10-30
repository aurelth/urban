import {
  ChangeDetectionStrategy,
  ChangeDetectorRef,
  Component,
  ElementRef,
  Injector,
  inject,
  signal,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButton } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { Router } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { AccountApi } from '../../core/auth/account.api';
import { SessionStore } from '../../core/auth/session.store';
import { toApiProblem } from '../../core/http/api-problem';
import { describeProblem } from '../../core/http/problem-message';
import { focusFirstInvalidInput } from '../../shared/forms/focus-first-invalid';
import { passwordPolicy } from '../../shared/forms/password.validator';
import { FormSubmit } from '../../shared/ui/form-submit/form-submit';
import { PasswordField } from '../../shared/ui/password-field/password.field';
import { CHANGE_EMAIL_MESSAGES, CHANGE_PASSWORD_MESSAGES } from './account.messages';
import { openChangeEmail } from './change-email.dialog';

/** A confirmação precisa ser igual à senha nova. Revalidada pela página sempre que a senha nova muda. */
function sameAsNewPassword(control: AbstractControl<string>): ValidationErrors | null {
  const next = control.parent?.get('newPassword')?.value;
  return control.value === '' || control.value === next ? null : { mismatch: true };
}

/**
 * "Minha conta": o que cada pessoa logada controla sobre o próprio acesso, o e-mail e a senha. Trocar a senha encerra
 * todas as sessões (a API revoga, e este navegador volta à entrada com um aviso). Trocar o e-mail é um pedido: o
 * endereço atual continua valendo até a pessoa abrir o link que chega ao endereço novo.
 */
@Component({
  selector: 'urban-account-page',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatCard,
    MatCardContent,
    MatIcon,
    PasswordField,
    FormSubmit,
  ],
  templateUrl: './account.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AccountPage {
  protected readonly session = inject(SessionStore);
  private readonly api = inject(AccountApi);
  private readonly router = inject(Router);
  private readonly injector = inject(Injector);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly changeDetector = inject(ChangeDetectorRef);

  protected readonly form = inject(NonNullableFormBuilder).group({
    currentPassword: ['', Validators.required],
    newPassword: ['', [Validators.required, passwordPolicy]],
    confirmPassword: ['', [Validators.required, sameAsNewPassword]],
  });
  protected readonly submitting = signal(false);
  protected readonly error = signal<string | null>(null);
  /** O endereço que a pessoa pediu para usar; aparece no aviso depois do pedido. */
  protected readonly requestedEmail = signal<string | null>(null);

  constructor() {
    this.form.controls.newPassword.valueChanges
      .pipe(takeUntilDestroyed())
      .subscribe(() => this.form.controls.confirmPassword.updateValueAndValidity());
  }

  protected async changePassword(): Promise<void> {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      // O foco procura a marca de campo inválido, que só chega ao HTML depois de a tela se atualizar.
      this.changeDetector.detectChanges();
      focusFirstInvalidInput(this.host.nativeElement);
      return;
    }

    this.submitting.set(true);
    this.error.set(null);
    try {
      const { currentPassword, newPassword } = this.form.getRawValue();
      await firstValueFrom(this.api.changePassword(currentPassword, newPassword));
      // A API revogou todas as sessões da conta: este navegador sai sem chamar a API de novo.
      this.session.endSession();
      await this.router.navigate(['/entrar'], { queryParams: { motivo: 'senha-alterada' } });
    } catch (error) {
      this.error.set(describeProblem(toApiProblem(error), CHANGE_PASSWORD_MESSAGES));
      this.form.controls.currentPassword.reset('');
    } finally {
      this.submitting.set(false);
    }
  }

  protected async changeEmail(): Promise<void> {
    const email = await openChangeEmail(this.injector, async (newEmail, password) => {
      try {
        await firstValueFrom(this.api.requestEmailChange(password, newEmail));
        return null;
      } catch (error) {
        return describeProblem(toApiProblem(error), CHANGE_EMAIL_MESSAGES);
      }
    });
    this.requestedEmail.set(email);
  }
}
