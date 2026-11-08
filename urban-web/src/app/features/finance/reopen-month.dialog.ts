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
import { monthTitle } from '../../core/finance/month';
import { openFinanceDialog } from './finance.dialog';

export interface ReopenMonthDialogConfig {
  /** O mês que vai reabrir, "AAAA-MM". */
  readonly month: string;
  /** Reabre o mês. Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: () => Promise<string | null>;
}

/** Confirma a reabertura de um mês fechado, avisando que as cobranças são apagadas e refeitas ao fechar de novo. */
@Component({
  selector: 'urban-reopen-month-dialog',
  imports: [MatButton, MatDialogActions, MatDialogContent, MatDialogTitle, MatIcon],
  templateUrl: './reopen-month.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReopenMonthDialog {
  protected readonly config = inject<ReopenMonthDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<ReopenMonthDialog, boolean>>(MatDialogRef);
  protected readonly title = monthTitle(this.config.month);

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

export function openReopenMonthDialog(
  injector: Injector,
  config: ReopenMonthDialogConfig,
): Promise<boolean> {
  return openFinanceDialog(injector, ReopenMonthDialog, config);
}
