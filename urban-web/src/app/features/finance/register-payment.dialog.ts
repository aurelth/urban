import { ChangeDetectionStrategy, Component, Injector, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { NOW } from '../../core/finance/clock';
import { formatMoney, formatMoneyInput, moneyField } from '../../core/finance/money';
import { formatMonth } from '../../core/finance/month';
import { PaymentMethod, RegisterPaymentPayload } from '../../core/finance/payments.models';
import { SelectField } from '../../shared/ui/select-field/select.field';
import { TextField } from '../../shared/ui/text-field/text.field';
import { openFinanceDialog } from './finance.dialog';
import { amountUpTo, paymentDate, registerPayload, todayText } from './payment.form';
import { PAYMENT_METHODS } from './payments-values';

export interface RegisterPaymentDialogConfig {
  /** A unidade, como aparece na lista ("Bloco A · 101"). */
  readonly label: string;
  /** A competência da cobrança, "AAAA-MM". */
  readonly month: string;
  /** O que falta pagar: o valor já vem preenchido e não pode ser passado. */
  readonly balance: number;
  /** Grava o pagamento. Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: (payload: RegisterPaymentPayload) => Promise<string | null>;
}

const NOTE_MAX_LENGTH = 200;

/** Registra um pagamento de uma cobrança: valor (já com o saldo), data do recebimento (já com hoje), forma e observação. */
@Component({
  selector: 'urban-register-payment-dialog',
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
  templateUrl: './register-payment.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class RegisterPaymentDialog {
  protected readonly config = inject<RegisterPaymentDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<RegisterPaymentDialog, boolean>>(MatDialogRef);
  private readonly now = inject(NOW);

  protected readonly form = inject(NonNullableFormBuilder).group({
    amount: [formatMoneyInput(this.config.balance), [moneyField, amountUpTo(this.config.balance)]],
    receivedOn: [
      todayText(this.now()),
      [Validators.required, paymentDate(this.config.month, this.now)],
    ],
    method: ['pix' as PaymentMethod],
    note: ['', Validators.maxLength(NOTE_MAX_LENGTH)],
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly methods = PAYMENT_METHODS;
  protected readonly balanceHint = `Saldo: ${formatMoney(this.config.balance)}`;
  protected readonly amountErrors = {
    money: 'Informe um valor maior que zero, com até 2 casas decimais',
    exceeds: `O valor passa do saldo de ${formatMoney(this.config.balance)}`,
  };
  protected readonly dateErrors = {
    required: 'Informe a data em que o pagamento foi recebido',
    date: 'Informe uma data que exista, como 10/11/2026',
    future: 'A data do recebimento não pode ser no futuro',
    tooOld: `A data do recebimento é anterior a 12 meses antes da competência (${formatMonth(this.config.month)})`,
  };
  protected readonly noteErrors = {
    maxlength: `A observação pode ter até ${NOTE_MAX_LENGTH} caracteres`,
  };

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
    const message = await this.config.save(registerPayload(this.form.getRawValue()));
    this.busy.set(false);

    if (message === null) {
      this.ref.close(true);
      return;
    }
    this.error.set(message);
  }
}

export function openRegisterPaymentDialog(
  injector: Injector,
  config: RegisterPaymentDialogConfig,
): Promise<boolean> {
  return openFinanceDialog(injector, RegisterPaymentDialog, config);
}
