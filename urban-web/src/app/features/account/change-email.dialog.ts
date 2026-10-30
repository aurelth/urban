import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  inject,
  signal,
  viewChild,
} from '@angular/core';
import {
  FormGroupDirective,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  Validators,
} from '@angular/forms';
import { MatButton } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { emailFormat } from '../../shared/forms/email.validator';
import { EmailField } from '../../shared/ui/email-field/email.field';
import { PasswordField } from '../../shared/ui/password-field/password.field';

/** Faz o pedido com o e-mail novo e a senha. Devolve nulo se deu certo ou o texto do erro para o diálogo mostrar. */
export type ChangeEmailRun = (newEmail: string, password: string) => Promise<string | null>;

interface ChangeEmailData {
  readonly run: ChangeEmailRun;
}

/**
 * Pede o e-mail novo e a senha da conta. A senha vive só no formulário do diálogo enquanto ele está aberto e é limpa a
 * cada erro. Quem decide se o pedido deu certo é `run`; o diálogo só mostra o erro e deixa tentar de novo.
 */
@Component({
  selector: 'urban-change-email-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatIcon,
    EmailField,
    PasswordField,
  ],
  templateUrl: './change-email.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChangeEmailDialog {
  private readonly data = inject<ChangeEmailData>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<ChangeEmailDialog, string>>(MatDialogRef);
  private readonly formDirective = viewChild.required(FormGroupDirective);

  protected readonly form = inject(NonNullableFormBuilder).group({
    email: ['', [Validators.required, emailFormat]],
    password: ['', Validators.required],
  });
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async confirm(): Promise<void> {
    if (this.busy()) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const { email, password } = this.form.getRawValue();
    this.busy.set(true);
    this.error.set(null);
    const message = await this.data.run(email.trim(), password);
    this.busy.set(false);

    if (message === null) {
      this.form.reset();
      this.ref.close(email.trim());
      return;
    }
    this.error.set(message);
    // Mantém o e-mail digitado, zera a senha e o "enviado" do formulário (senão o campo vazio já nasceria com erro).
    this.formDirective().resetForm({ email, password: '' });
  }
}

/** Abre o diálogo e responde com o e-mail pedido, ou nulo se a pessoa cancelou. */
export function openChangeEmail(injector: Injector, run: ChangeEmailRun): Promise<string | null> {
  const ref = injector
    .get(MatDialog)
    .open<ChangeEmailDialog, ChangeEmailData, string>(ChangeEmailDialog, {
      data: { run },
      disableClose: true,
      role: 'dialog',
      width: '440px',
      maxWidth: 'calc(100vw - 32px)',
    });
  return new Promise<string | null>((resolve) => {
    ref.afterClosed().subscribe((email) => resolve(email ?? null));
  });
}
