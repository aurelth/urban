import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { ActivatedRoute } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { NOW } from '../../core/finance/clock';
import { InvoicesApi } from '../../core/finance/invoices.api';
import { formatMoney } from '../../core/finance/money';
import { formatDate, monthTitle } from '../../core/finance/month';
import { MyUnit } from '../../core/finance/my-month.models';
import { toApiProblem } from '../../core/http/api-problem';
import { describeProblem } from '../../core/http/problem-message';
import { FileSaver } from '../../shared/files/file-saver';
import { groupShare } from '../finance/month-dashboard-values';
import { chargeStatusView } from '../finance/payments-values';
import { MonthNavigation } from '../finance/month-navigation';
import { MY_INVOICE_MESSAGES, unitHeading } from './my-month-values';
import { MyMonthStore } from './my-month.store';

/**
 * A fatura do morador: um cartão por unidade dele, com a cobrança do mês e o PDF, e as despesas do condomínio no mês
 * (sem fornecedor nem observação). Mês ainda aberto explica que a fatura aparece depois do fechamento; dependente e quem
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
  private readonly files = inject(FileSaver);

  protected readonly nav = new MonthNavigation(
    this.route.snapshot.queryParamMap.get('mes'),
    this.now,
    (month) => this.show(month),
  );
  protected readonly month = this.nav.month;
  protected readonly notice = signal<string | null>(null);
  protected readonly invoiceError = signal<string | null>(null);
  /** A cobrança cujo PDF está sendo baixado. */
  protected readonly downloading = signal<string | null>(null);

  protected readonly title = monthTitle;
  protected readonly money = formatMoney;
  protected readonly date = formatDate;
  protected readonly share = groupShare;
  protected readonly heading = unitHeading;
  protected readonly statusView = chargeStatusView;

  ngOnInit(): void {
    void this.nav.start();
  }

  protected async downloadInvoice(unit: MyUnit): Promise<void> {
    if (unit.charge === null) {
      return;
    }
    this.clearMessages();
    this.downloading.set(unit.charge.chargeId);
    try {
      const { blob, fileName } = await firstValueFrom(
        this.invoices.downloadMine(unit.charge.chargeId),
      );
      this.files.save(blob, fileName);
      this.notice.set(`Fatura de ${unitHeading(unit)} baixada`);
    } catch (error) {
      this.invoiceError.set(describeProblem(toApiProblem(error), MY_INVOICE_MESSAGES));
    } finally {
      this.downloading.set(null);
    }
  }

  private show(month: string): Promise<void> {
    this.clearMessages();
    return this.store.show(month);
  }

  private clearMessages(): void {
    this.notice.set(null);
    this.invoiceError.set(null);
  }
}
