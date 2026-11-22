import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NOW } from '../../core/finance/clock';
import { formatMoney } from '../../core/finance/money';
import { formatDate, monthTitle } from '../../core/finance/month';
import { receivedPercent } from '../dashboard/dashboard-values';
import {
  dashboardMonthState,
  expenseAttentions,
  groupShare,
  overdueChargesText,
  unitSituations,
} from './month-dashboard-values';
import { MonthDashboardStore } from './month-dashboard.store';
import { MonthNavigation } from './month-navigation';

/**
 * O painel financeiro de um mês: as despesas por grupo e os avisos (sem valor, vencidas) e, com o mês fechado, as cobranças:
 * recebido do cobrado, unidades por situação e o que está atrasado. Mês ainda não fechado explica que as cobranças aparecem
 * depois do fechamento. As setas navegam entre os meses, na mesma janela das outras telas do Financeiro.
 */
@Component({
  selector: 'urban-month-dashboard-page',
  imports: [RouterLink, MatButton, MatIconButton, MatIcon],
  templateUrl: './month-dashboard.page.html',
  providers: [MonthDashboardStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MonthDashboardPage implements OnInit {
  protected readonly store = inject(MonthDashboardStore);
  private readonly now = inject(NOW);
  private readonly route = inject(ActivatedRoute);

  protected readonly nav = new MonthNavigation(
    this.route.snapshot.queryParamMap.get('mes'),
    this.now,
    (month) => this.store.show(month),
  );
  protected readonly month = this.nav.month;

  protected readonly title = monthTitle;
  protected readonly money = formatMoney;
  protected readonly date = formatDate;
  protected readonly state = dashboardMonthState;
  protected readonly share = groupShare;
  protected readonly situations = unitSituations;
  protected readonly attentions = expenseAttentions;
  protected readonly overdueText = overdueChargesText;
  protected readonly percent = receivedPercent;

  ngOnInit(): void {
    void this.nav.start();
  }

  /** A quantia que falta receber no mês, em centavos exatos. */
  protected toReceive(charged: number, received: number): number {
    return Math.round((charged - received) * 100) / 100;
  }
}
