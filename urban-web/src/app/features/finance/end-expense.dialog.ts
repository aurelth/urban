import { ChangeDetectionStrategy, Component, Injector, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
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
import { addMonths, monthOptions } from '../../core/finance/month';
import { SelectField } from '../../shared/ui/select-field/select.field';
import { openFinanceDialog } from './finance.dialog';

const MONTHS_AHEAD = 24;

export interface EndExpenseDialogConfig {
  readonly expense: Expense;
  /** O mês de hoje ("AAAA-MM"), que serve de sugestão para o último mês. */
  readonly month: string;
  /** Grava o último mês. Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: (lastMonth: string) => Promise<string | null>;
}

/** Encerra uma despesa fixa ou variável: ela deixa de gerar lançamentos depois do último mês escolhido. */
@Component({
  selector: 'urban-end-expense-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatIcon,
    SelectField,
  ],
  templateUrl: './end-expense.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EndExpenseDialog {
  protected readonly config = inject<EndExpenseDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<EndExpenseDialog, boolean>>(MatDialogRef);

  protected readonly months = monthOptions(
    this.config.expense.startMonth,
    addMonths(this.config.month, MONTHS_AHEAD),
  );

  protected readonly form = inject(NonNullableFormBuilder).group({
    lastMonth: [
      this.config.month < this.config.expense.startMonth
        ? this.config.expense.startMonth
        : this.config.month,
    ],
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async confirm(): Promise<void> {
    if (this.busy()) {
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    const message = await this.config.save(this.form.getRawValue().lastMonth);
    this.busy.set(false);

    if (message === null) {
      this.ref.close(true);
      return;
    }
    this.error.set(message);
  }
}

export function openEndExpenseDialog(
  injector: Injector,
  config: EndExpenseDialogConfig,
): Promise<boolean> {
  return openFinanceDialog(injector, EndExpenseDialog, config);
}
