import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { ActivatedRoute } from '@angular/router';
import { NOW } from '../../core/finance/clock';
import { InvoicesApi } from '../../core/finance/invoices.api';
import { formatMoney } from '../../core/finance/money';
import { formatDate, monthTitle } from '../../core/finance/month';
import { MyPayment, MyUnit } from '../../core/finance/my-month.models';
import { ReceiptsApi } from '../../core/finance/receipts.api';
import { FileDownloads } from '../../shared/files/file-downloads';
import { FileSaver } from '../../shared/files/file-saver';
import { groupShare } from '../finance/month-dashboard-values';
import { chargeStatusView, methodLabel, receiptLabel } from '../finance/payments-values';
import { MonthNavigation } from '../finance/month-navigation';
import { MY_INVOICE_MESSAGES, MY_RECEIPT_MESSAGES, unitHeading } from './my-month-values';
import { MyMonthStore } from './my-month.store';

/**
 * A fatura do morador: um cartão por unidade dele, com a cobrança do mês, o PDF, os pagamentos e o recibo de cada um, e as
 * despesas do condomínio no mês (sem fornecedor nem observação). Mês ainda aberto explica que a fatura aparece depois do fechamento; dependente e quem
 * não tem unidade recebem um aviso. As setas navegam entre os meses, na mesma janela das telas do Financeiro.
 */
@Component({
  selector: 'urban-my-month-page',
  imports: [MatButton, MatIconButton, MatIcon],
  templateUrl: './my-month.page.html',
  providers: [MyMonthStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MyMonthPage implements OnInit {
  protected readonly store = inject(MyMonthStore);
  private readonly now = inject(NOW);
  private readonly route = inject(ActivatedRoute);
  private readonly invoices = inject(InvoicesApi);
  private readonly receipts = inject(ReceiptsApi);

  protected readonly nav = new MonthNavigation(
    this.route.snapshot.queryParamMap.get('mes'),
    this.now,
    (month) => this.show(month),
  );
  protected readonly month = this.nav.month;
  protected readonly notice = signal<string | null>(null);
  /** Um download por vez, de fatura ou de recibo; a chave é o id da cobrança ou do pagamento. */
  protected readonly downloads = new FileDownloads(inject(FileSaver), {
    ...MY_INVOICE_MESSAGES,
    ...MY_RECEIPT_MESSAGES,
  });

  protected readonly title = monthTitle;
  protected readonly money = formatMoney;
  protected readonly date = formatDate;
  protected readonly share = groupShare;
  protected readonly heading = unitHeading;
  protected readonly statusView = chargeStatusView;
  protected readonly method = methodLabel;

  protected receiptText(payment: MyPayment): string {
    return receiptLabel(payment.receiptCode, payment.reversed);
  }

  ngOnInit(): void {
    void this.nav.start();
  }

  protected async downloadInvoice(unit: MyUnit): Promise<void> {
    if (unit.charge === null) {
      return;
    }
    this.clearMessages();
    if (
      await this.downloads.run(
        unit.charge.chargeId,
        this.invoices.downloadMine(unit.charge.chargeId),
      )
    ) {
      this.notice.set(`Fatura de ${unitHeading(unit)} baixada`);
    }
  }

  protected async downloadReceipt(payment: MyPayment): Promise<void> {
    this.clearMessages();
    if (
      await this.downloads.run(payment.paymentId, this.receipts.downloadMine(payment.paymentId))
    ) {
      this.notice.set(`Recibo ${payment.receiptCode} baixado`);
    }
  }

  private show(month: string): Promise<void> {
    this.clearMessages();
    return this.store.show(month);
  }

  private clearMessages(): void {
    this.notice.set(null);
    this.downloads.clear();
  }
}
