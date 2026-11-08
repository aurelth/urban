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
import { formatMoneyInput, moneyField, parseMoney } from '../../core/finance/money';
import { addMonths, monthOptions } from '../../core/finance/month';
import { SelectField } from '../../shared/ui/select-field/select.field';
import { TextField } from '../../shared/ui/text-field/text.field';
import { openFinanceDialog } from './finance.dialog';

const MONTHS_AHEAD = 24;

export interface ExpenseAmountDialogConfig {
  readonly expense: Expense;
  /** O mês de hoje ("AAAA-MM"), que serve de sugestão para o início da vigência. */
  readonly month: string;
  /** Grava o novo valor. Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: (fromMonth: string, amount: number) => Promise<string | null>;
}

/** Reajusta uma despesa fixa: o novo valor vale a partir de um mês, e os anteriores continuam como estavam. */
@Component({
  selector: 'urban-expense-amount-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatIcon,
    SelectField,
    TextField,
  ],
  templateUrl: './expense-amount.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExpenseAmountDialog {
  protected readonly config = inject<ExpenseAmountDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<ExpenseAmountDialog, boolean>>(MatDialogRef);

  protected readonly months = monthOptions(
    this.config.expense.startMonth,
    addMonths(this.config.month, MONTHS_AHEAD),
  );

  protected readonly form = inject(NonNullableFormBuilder).group({
    fromMonth: [
      this.config.month < this.config.expense.startMonth
        ? this.config.expense.startMonth
        : this.config.month,
    ],
    amount: [formatMoneyInput(this.config.expense.currentAmount), moneyField],
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly amountErrors = {
    money: 'Informe um valor maior que zero, com até 2 casas decimais',
  };

  protected async confirm(): Promise<void> {
    if (this.busy()) {
      return;
    }
    const value = this.form.getRawValue();
    const amount = parseMoney(value.amount);
    if (this.form.invalid || amount === null) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    const message = await this.config.save(value.fromMonth, amount);
    this.busy.set(false);

    if (message === null) {
      this.ref.close(true);
      return;
    }
    this.error.set(message);
  }
}

export function openExpenseAmountDialog(
  injector: Injector,
  config: ExpenseAmountDialogConfig,
): Promise<boolean> {
  return openFinanceDialog(injector, ExpenseAmountDialog, config);
}
