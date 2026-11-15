import { ChangeDetectionStrategy, Component, Injector, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { Expense } from '../../core/finance/finance.models';
import { openFinanceDialog } from './finance.dialog';

export interface DeleteExpenseDialogConfig {
  readonly expense: Expense;
  /** Exclui a despesa. Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: () => Promise<string | null>;
}

/** Confirma a exclusão de uma despesa, que leva junto todos os lançamentos dela e não se desfaz. */
@Component({
  selector: 'urban-delete-expense-dialog',
  imports: [MatButton, MatDialogActions, MatDialogContent, MatDialogTitle, MatIcon],
  templateUrl: './delete-expense.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeleteExpenseDialog {
  protected readonly config = inject<DeleteExpenseDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<DeleteExpenseDialog, boolean>>(MatDialogRef);

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async confirm(): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const message = await this.config.save();
    this.busy.set(false);

    if (message === null) {
      this.ref.close(true);
      return;
    }
    this.error.set(message);
  }
}

export function openDeleteExpenseDialog(
  injector: Injector,
  config: DeleteExpenseDialogConfig,
): Promise<boolean> {
  return openFinanceDialog(injector, DeleteExpenseDialog, config);
}
