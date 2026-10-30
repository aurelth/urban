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
import { PasswordField } from '../password-field/password.field';

/** O que o diálogo mostra e o que ele faz com a senha digitada. */
export interface PasswordConfirmConfig {
  readonly title: string;
  readonly description: string;
  readonly confirmLabel: string;
  readonly busyLabel: string;
  /**
   * Faz a ação protegida com a senha digitada. Devolve nulo quando deu certo (o diálogo fecha) ou o texto do erro (o
   * diálogo continua aberto, com a senha limpa, para a pessoa tentar de novo).
   */
  readonly run: (password: string) => Promise<string | null>;
}

/**
 * Pede a senha da conta antes de uma ação sensível. A senha mora só no controle do formulário do diálogo, enquanto ele
 * está aberto: é limpa a cada erro e nunca vai para a tela, para a URL nem para o armazenamento.
 */
@Component({
  selector: 'urban-password-confirm-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatIcon,
    PasswordField,
  ],
  templateUrl: './password-confirm.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class PasswordConfirmDialog {
  protected readonly config = inject<PasswordConfirmConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<PasswordConfirmDialog, boolean>>(MatDialogRef);
  private readonly formDirective = viewChild.required(FormGroupDirective);

  protected readonly form = inject(NonNullableFormBuilder).group({
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

    this.busy.set(true);
    this.error.set(null);
    const message = await this.config.run(this.form.getRawValue().password);
    this.busy.set(false);

    if (message === null) {
      this.form.reset();
      this.ref.close(true);
      return;
    }
    this.error.set(message);
    // Zera também o "enviado" do formulário, senão o campo vazio já nasceria com o erro de obrigatório.
    this.formDirective().resetForm();
  }
}

/**
 * Abre o diálogo como modal que não fecha ao clicar fora nem com Esc, para ninguém perder o que digitou sem querer.
 * Responde verdadeiro quando a ação deu certo e falso quando a pessoa cancelou.
 */
export function openPasswordConfirm(
  injector: Injector,
  config: PasswordConfirmConfig,
): Promise<boolean> {
  const ref = injector
    .get(MatDialog)
    .open<PasswordConfirmDialog, PasswordConfirmConfig, boolean>(PasswordConfirmDialog, {
      data: config,
      disableClose: true,
      role: 'dialog',
      width: '440px',
      maxWidth: 'calc(100vw - 32px)',
    });
  return new Promise<boolean>((resolve) => {
    ref.afterClosed().subscribe((confirmed) => resolve(confirmed === true));
  });
}
