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
import { Expense, Installment } from '../../core/finance/finance.models';
import { formatMoney, formatMoneyInput, moneyField, parseMoney } from '../../core/finance/money';
import { formatMonth } from '../../core/finance/month';
import { SelectField } from '../../shared/ui/select-field/select.field';
import { TextField } from '../../shared/ui/text-field/text.field';
import { openFinanceDialog } from './finance.dialog';

export interface InstallmentDialogConfig {
  readonly expense: Expense;
  /** O mês de hoje ("AAAA-MM"): a primeira parcela que ainda vem é a sugerida. */
  readonly month: string;
  /** Grava o valor da parcela. Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: (number: number, amount: number) => Promise<string | null>;
}

/** Muda o valor de uma parcela de uma despesa parcelada. O botão só habilita quando o valor muda. */
@Component({
  selector: 'urban-installment-dialog',
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
  templateUrl: './installment.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class InstallmentDialog {
  protected readonly config = inject<InstallmentDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<InstallmentDialog, boolean>>(MatDialogRef);

  protected readonly parcels = this.config.expense.installments.map((parcel) => ({
    value: String(parcel.number),
    label: `Parcela ${parcel.number} de ${this.config.expense.installmentCount} · ${formatMonth(parcel.month)} · ${formatMoney(parcel.amount)}`,
  }));

  private readonly first = this.suggested();

  protected readonly form = inject(NonNullableFormBuilder).group({
    installment: [String(this.first.number)],
    amount: [formatMoneyInput(this.first.amount), moneyField],
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly changed = signal(false);

  protected readonly amountErrors = {
    money: 'Informe um valor maior que zero, com até 2 casas decimais',
  };

  constructor() {
    this.form.controls.installment.valueChanges.subscribe((number) => {
      const parcel = this.parcelOf(number);
      this.form.controls.amount.setValue(formatMoneyInput(parcel?.amount ?? null));
    });
    this.form.valueChanges.subscribe(() => {
      const value = this.form.getRawValue();
      const parcel = this.parcelOf(value.installment);
      this.changed.set(value.amount.trim() !== formatMoneyInput(parcel?.amount ?? null));
    });
  }

  protected async confirm(): Promise<void> {
    if (this.busy() || !this.changed()) {
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
    const message = await this.config.save(Number(value.installment), amount);
    this.busy.set(false);

    if (message === null) {
      this.ref.close(true);
      return;
    }
    this.error.set(message);
  }

  private parcelOf(number: string): Installment | undefined {
    return this.config.expense.installments.find((parcel) => String(parcel.number) === number);
  }

  private suggested(): Installment {
    const parcels = this.config.expense.installments;
    return (
      parcels.find((parcel) => parcel.month >= this.config.month) ?? parcels[parcels.length - 1]
    );
  }
}

export function openInstallmentDialog(
  injector: Injector,
  config: InstallmentDialogConfig,
): Promise<boolean> {
  return openFinanceDialog(injector, InstallmentDialog, config);
}
