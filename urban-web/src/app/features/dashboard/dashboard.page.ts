import { ChangeDetectionStrategy, Component, OnInit, computed, inject } from '@angular/core';
import { MatAnchor, MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { SessionStore } from '../../core/auth/session.store';
import { NOW } from '../../core/finance/clock';
import { formatDate, currentMonth, monthTitle } from '../../core/finance/month';
import { formatMoney } from '../../core/finance/money';
import { DashboardCard } from './dashboard-card';
import { MonthState, monthState, receivedPercent } from './dashboard-values';
import { DashboardStore } from './dashboard.store';

const MONTH_STATE_LABELS: Record<MonthState, string> = {
  'not-open': 'Ainda não aberto',
  open: 'Aberto',
  closed: 'Fechado',
};

/** O painel do síndico: o mês, os recebimentos, o condomínio e o que precisa de atenção. */
@Component({
  selector: 'urban-dashboard-page',
  imports: [RouterLink, MatAnchor, MatButton, MatIcon, DashboardCard],
  providers: [DashboardStore],
  templateUrl: './dashboard.page.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DashboardPage implements OnInit {
  protected readonly store = inject(DashboardStore);
  private readonly session = inject(SessionStore);
  private readonly now = inject(NOW);

  protected readonly isManager = computed(() => this.session.user()?.role === 'Manager');
  protected readonly month = currentMonth(this.now());
  protected readonly monthLabel = monthTitle(this.month);

  protected readonly monthState = monthState;
  protected readonly monthStateLabel = (state: MonthState): string => MONTH_STATE_LABELS[state];
  protected readonly receivedPercent = receivedPercent;
  protected readonly overdueChargesText = (count: number): string => {
    if (count === 0) {
      return 'Nenhuma cobrança em atraso';
    }
    return `${String(count)} ${count === 1 ? 'cobrança em atraso' : 'cobranças em atraso'}`;
  };
  protected readonly formatMoney = formatMoney;
  protected readonly formatDate = formatDate;

  ngOnInit(): void {
    if (this.isManager()) {
      void this.store.load(this.month);
    }
  }
}
