import { ChangeDetectionStrategy, Component, Injector, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatFormField, MatHint, MatError, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { Manager } from '../../../core/admin/admin.models';
import { requiredText } from '../../../shared/forms/required-text.validator';
import { managerLabel } from './manager-name';

/** Mesmo limite da API; passar dele é recusado lá com `identity.user.reason_too_long`. */
export const REASON_MAX_LENGTH = 500;

/** O que o diálogo mostra e o que ele faz com o motivo digitado. */
export interface SuspendManagerDialogConfig {
  readonly manager: Manager;
  /**
   * Suspende o síndico. Devolve nulo quando deu certo (o diálogo fecha) ou o texto do erro (o diálogo continua aberto,
   * com o motivo digitado).
   */
  readonly save: (reason: string) => Promise<string | null>;
}

/** Pede o motivo da suspensão. Ele fica registrado e aparece na lista; reativar a conta é um clique. */
@Component({
  selector: 'urban-suspend-manager-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatError,
    MatFormField,
    MatHint,
    MatIcon,
    MatInput,
    MatLabel,
  ],
  templateUrl: './suspend-manager.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class SuspendManagerDialog {
  protected readonly config = inject<SuspendManagerDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<SuspendManagerDialog, boolean>>(MatDialogRef);
  protected readonly label = managerLabel(this.config.manager);
  protected readonly maxLength = REASON_MAX_LENGTH;

  protected readonly form = inject(NonNullableFormBuilder).group({
    reason: ['', [requiredText, Validators.maxLength(REASON_MAX_LENGTH)]],
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
    const message = await this.config.save(this.form.getRawValue().reason.trim());
    this.busy.set(false);

    if (message === null) {
      this.ref.close(true);
      return;
    }
    this.error.set(message);
  }
}

/**
 * Abre o diálogo como modal que não fecha ao clicar fora nem com Esc, para ninguém perder o motivo digitado sem querer.
 * Responde verdadeiro quando o síndico foi suspenso e falso quando a pessoa cancelou.
 */
export function openSuspendManagerDialog(
  injector: Injector,
  config: SuspendManagerDialogConfig,
): Promise<boolean> {
  const ref = injector
    .get(MatDialog)
    .open<SuspendManagerDialog, SuspendManagerDialogConfig, boolean>(SuspendManagerDialog, {
      data: config,
      disableClose: true,
      role: 'dialog',
      width: '520px',
      maxWidth: 'calc(100vw - 32px)',
    });
  return new Promise<boolean>((resolve) => {
    ref.afterClosed().subscribe((suspended) => resolve(suspended === true));
  });
}
