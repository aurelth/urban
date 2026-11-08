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
import { ExpenseEntry } from '../../core/finance/finance.models';
import { formatMoneyInput, moneyField, parseMoney } from '../../core/finance/money';
import { TextField } from '../../shared/ui/text-field/text.field';
import { openFinanceDialog } from './finance.dialog';

export interface EntryAmountDialogConfig {
  readonly entry: ExpenseEntry;
  /** Grava o valor. Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: (amount: number) => Promise<string | null>;
}

/**
 * Informa o valor de um lançamento pendente (já sugerindo o do mês anterior) ou corrige o de um lançamento que já
 * tem valor. Vale só para aquele mês. Na correção, o botão só habilita depois de mudar o valor.
 */
@Component({
  selector: 'urban-entry-amount-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatIcon,
    TextField,
  ],
  templateUrl: './entry-amount.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class EntryAmountDialog {
  protected readonly config = inject<EntryAmountDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<EntryAmountDialog, boolean>>(MatDialogRef);
  protected readonly pending = this.config.entry.isPending;
  protected readonly suggested = this.pending && this.config.entry.suggestedAmount !== null;

  private readonly initial = formatMoneyInput(
    this.config.entry.amount ?? this.config.entry.suggestedAmount,
  );

  protected readonly form = inject(NonNullableFormBuilder).group({
    amount: [this.initial, moneyField],
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly changed = signal(false);

  protected readonly amountErrors = {
    money: 'Informe um valor maior que zero, com até 2 casas decimais',
  };

  constructor() {
    this.form.valueChanges.subscribe(() => {
      this.changed.set(this.form.getRawValue().amount.trim() !== this.initial);
    });
  }

  protected async confirm(): Promise<void> {
    if (this.busy() || (!this.pending && !this.changed())) {
      return;
    }
    const amount = parseMoney(this.form.getRawValue().amount);
    if (this.form.invalid || amount === null) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    const message = await this.config.save(amount);
    this.busy.set(false);

    if (message === null) {
      this.ref.close(true);
      return;
    }
    this.error.set(message);
  }
}

export function openEntryAmountDialog(
  injector: Injector,
  config: EntryAmountDialogConfig,
): Promise<boolean> {
  return openFinanceDialog(injector, EntryAmountDialog, config);
}
