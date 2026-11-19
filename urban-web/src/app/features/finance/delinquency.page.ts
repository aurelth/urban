import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { Debtor, OverdueCharge } from '../../core/finance/delinquency.models';
import { formatMoney } from '../../core/finance/money';
import { formatDate, monthTitle } from '../../core/finance/month';
import { ApiProblem } from '../../core/http/api-problem';
import { describeProblem } from '../../core/http/problem-message';
import { unitLabel } from '../units/unit-values';
import { daysText, debtorSummary } from './delinquency-values';
import { DelinquencyStore } from './delinquency.store';
import { INVOICE_DELIVERY_MESSAGES } from './invoice-deliveries.messages';

/**
 * Inadimplência: o total em atraso e as unidades com cobrança vencida e ainda não paga, da que mais deve para a que menos
 * deve, cada uma com as cobranças em atraso. Em cada cobrança, reenviar a fatura por e-mail; o pagamento se registra na tela
 * de pagamentos.
 */
@Component({
  selector: 'urban-delinquency-page',
  imports: [RouterLink, MatButton, MatIcon],
  templateUrl: './delinquency.page.html',
  providers: [DelinquencyStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DelinquencyPage implements OnInit {
  protected readonly store = inject(DelinquencyStore);

  protected readonly notice = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);
  /** A cobrança cuja fatura está sendo reenviada; só uma por vez. */
  protected readonly sending = signal<string | null>(null);

  protected readonly money = formatMoney;
  protected readonly date = formatDate;
  protected readonly title = monthTitle;
  protected readonly unitLabel = unitLabel;
  protected readonly summary = debtorSummary;
  protected readonly days = daysText;

  ngOnInit(): void {
    void this.store.load();
  }

  protected reload(): Promise<void> {
    this.clearMessages();
    return this.store.load();
  }

  protected goTo(page: number): void {
    this.clearMessages();
    void this.store.goToPage(page);
  }

  protected async resend(debtor: Debtor, charge: OverdueCharge): Promise<void> {
    this.clearMessages();
    this.sending.set(charge.chargeId);
    const problem = await this.store.resend(charge.chargeId);
    this.sending.set(null);
    if (problem === null) {
      this.notice.set(
        `Fatura de ${unitLabel(debtor)} (${monthTitle(charge.month)}) entrou na fila de envio`,
      );
    } else {
      this.fail(problem);
    }
  }

  private fail(problem: ApiProblem): void {
    this.actionError.set(describeProblem(problem, INVOICE_DELIVERY_MESSAGES));
  }

  private clearMessages(): void {
    this.notice.set(null);
    this.actionError.set(null);
  }
}
