import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { firstValueFrom } from 'rxjs';
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
import { ReceiptsApi } from '../../core/finance/receipts.api';
import { toApiProblem } from '../../core/http/api-problem';
import { describeProblem } from '../../core/http/problem-message';
import { FileDownloads } from '../../shared/files/file-downloads';
import { FileSaver } from '../../shared/files/file-saver';
import { requiredText } from '../../shared/forms/required-text.validator';
import { trimmedLength } from '../../shared/forms/text-length.validator';
import { TextField } from '../../shared/ui/text-field/text.field';
import { formatRequestedAt } from '../admin/registration-requests/requested-at';
import { formatMoney } from '../../core/finance/money';
import { ChargePaymentsStore } from './charge-payments.store';
import { openFinanceDialog } from './finance.dialog';
import { chargeStatusView, methodLabel, receiptLabel } from './payments-values';
import { REVERSE_PAYMENT_MESSAGES } from './payments.messages';
import { RECEIPT_MESSAGES } from './receipt.messages';

export interface ChargePaymentsDialogConfig {
  readonly chargeId: string;
  /** A unidade, como aparece na lista ("Bloco A · 101"). */
  readonly label: string;
}

const REASON_MAX_LENGTH = 200;
const STALE_STATUSES: readonly number[] = [404, 409];

/**
 * Os pagamentos de uma cobrança, do mais novo para o mais antigo, estornados inclusive. Cada pagamento ativo tem "Estornar",
 * que abre no próprio item o motivo (obrigatório), e todo pagamento, o estornado também, tem "Baixar recibo"; o ativo tem ainda "Enviar por e-mail" (o estornado não: o recibo dele fica cancelado). Fecha
 * respondendo verdadeiro quando algo mudou (baixar ou enviar um recibo não muda nada, salvo quando a recusa mostra que a lista está velha), para a lista do mês ser lida de novo.
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
  private readonly receipts = inject(ReceiptsApi);
  protected readonly downloads = new FileDownloads(inject(FileSaver), RECEIPT_MESSAGES);

  protected readonly form = inject(NonNullableFormBuilder).group({
    reason: ['', [requiredText, trimmedLength(1, REASON_MAX_LENGTH)]],
  });

  protected readonly reversing = signal<string | null>(null);
  protected readonly reverseError = signal<string | null>(null);
  protected readonly notice = signal<string | null>(null);
  protected readonly receiptNotice = signal<string | null>(null);
  protected readonly emailing = signal<string | null>(null);
  protected readonly emailNotice = signal<string | null>(null);
  protected readonly emailError = signal<string | null>(null);
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

  protected receiptText(payment: Payment): string {
    return receiptLabel(payment.receiptCode, payment.reversedAt !== null);
  }

  protected info(payment: Payment): string {
    return [formatDate(payment.receivedOn), methodLabel(payment.method), payment.note]
      .filter((part) => part !== null)
      .join(' · ');
  }

  protected async downloadReceipt(payment: Payment): Promise<void> {
    this.receiptNotice.set(null);
    this.clearEmailFeedback();
    this.notice.set(null);
    if (await this.downloads.run(payment.id, this.receipts.download(payment.id))) {
      this.receiptNotice.set(`Recibo ${payment.receiptCode} baixado`);
    }
  }

  protected async emailReceipt(payment: Payment): Promise<void> {
    this.clearEmailFeedback();
    this.receiptNotice.set(null);
    this.downloads.clear();
    this.notice.set(null);
    this.emailing.set(payment.id);
    try {
      const sent = await firstValueFrom(this.receipts.sendByEmail(payment.id));
      this.emailNotice.set(`Recibo ${payment.receiptCode} enviado para ${sent.recipientName}`);
    } catch (failure) {
      const problem = toApiProblem(failure);
      if (STALE_STATUSES.includes(problem.status)) {
        this.changed.set(true);
      }
      this.emailError.set(describeProblem(problem, RECEIPT_MESSAGES));
    } finally {
      this.emailing.set(null);
    }
  }

  protected startReversal(payment: Payment): void {
    this.form.reset();
    this.notice.set(null);
    this.receiptNotice.set(null);
    this.clearEmailFeedback();
    this.downloads.clear();
    this.reverseError.set(null);
    this.reversing.set(payment.id);
  }

  private clearEmailFeedback(): void {
    this.emailNotice.set(null);
    this.emailError.set(null);
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
