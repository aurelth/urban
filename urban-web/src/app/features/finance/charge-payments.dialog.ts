import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  OnInit,
  inject,
  signal,
} from '@angular/core';
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
import { formatDate } from '../../core/finance/month';
import { Payment } from '../../core/finance/payments.models';
import { describeProblem } from '../../core/http/problem-message';
import { requiredText } from '../../shared/forms/required-text.validator';
import { trimmedLength } from '../../shared/forms/text-length.validator';
import { TextField } from '../../shared/ui/text-field/text.field';
import { formatRequestedAt } from '../admin/registration-requests/requested-at';
import { formatMoney } from '../../core/finance/money';
import { ChargePaymentsStore } from './charge-payments.store';
import { openFinanceDialog } from './finance.dialog';
import { chargeStatusView, methodLabel } from './payments-values';
import { REVERSE_PAYMENT_MESSAGES } from './payments.messages';

export interface ChargePaymentsDialogConfig {
  readonly chargeId: string;
  /** A unidade, como aparece na lista ("Bloco A · 101"). */
  readonly label: string;
}

const REASON_MAX_LENGTH = 200;
const STALE_STATUSES: readonly number[] = [404, 409];

/**
 * Os pagamentos de uma cobrança, do mais novo para o mais antigo, estornados inclusive. Cada pagamento ativo tem "Estornar",
 * que abre no próprio item o motivo (obrigatório). Fecha respondendo verdadeiro quando algo mudou, para a lista do mês ser
 * lida de novo.
 */
@Component({
  selector: 'urban-charge-payments-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatIcon,
    TextField,
  ],
  templateUrl: './charge-payments.dialog.html',
  providers: [ChargePaymentsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ChargePaymentsDialog implements OnInit {
  protected readonly config = inject<ChargePaymentsDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<ChargePaymentsDialog, boolean>>(MatDialogRef);
  protected readonly store = inject(ChargePaymentsStore);

  protected readonly form = inject(NonNullableFormBuilder).group({
    reason: ['', [requiredText, trimmedLength(1, REASON_MAX_LENGTH)]],
  });

  protected readonly reversing = signal<string | null>(null);
  protected readonly reverseError = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly changed = signal(false);

  protected readonly money = formatMoney;
  protected readonly statusView = chargeStatusView;
  protected readonly reversedAt = formatRequestedAt;
  protected readonly reasonErrors = {
    required: 'Informe o motivo do estorno',
    tooLong: `O motivo pode ter até ${REASON_MAX_LENGTH} caracteres`,
  };

  ngOnInit(): void {
    void this.reload();
  }

  protected reload(): Promise<void> {
    return this.store.load(this.config.chargeId);
  }

  protected info(payment: Payment): string {
    return [formatDate(payment.receivedOn), methodLabel(payment.method), payment.note]
      .filter((part) => part !== null)
      .join(' · ');
  }

  protected startReversal(payment: Payment): void {
    this.form.reset();
    this.notice.set(null);
    this.reverseError.set(null);
    this.reversing.set(payment.id);
  }

  protected cancelReversal(): void {
    this.reversing.set(null);
    this.reverseError.set(null);
  }

  protected async confirmReversal(): Promise<void> {
    const paymentId = this.reversing();
    if (paymentId === null || this.store.busy()) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.reverseError.set(null);
    const problem = await this.store.reverse(paymentId, this.form.getRawValue().reason.trim());
    if (problem === null) {
      this.changed.set(true);
      this.reversing.set(null);
      this.notice.set('Pagamento estornado');
      return;
    }
    if (STALE_STATUSES.includes(problem.status)) {
      this.changed.set(true);
    }
    this.reverseError.set(describeProblem(problem, REVERSE_PAYMENT_MESSAGES));
  }
}

export function openChargePaymentsDialog(
  injector: Injector,
  config: ChargePaymentsDialogConfig,
): Promise<boolean> {
  return openFinanceDialog(injector, ChargePaymentsDialog, config, '640px');
}
