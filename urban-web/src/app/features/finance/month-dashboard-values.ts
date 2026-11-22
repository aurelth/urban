import {
  DashboardCharges,
  DashboardExpenses,
  MonthDashboard,
} from '../../core/finance/month-dashboard.models';
import { ChargeStatus } from '../../core/finance/payments.models';
import { overdueChargesText as countOverdueCharges } from './delinquency-values';
import { chargeStatusView } from './payments-values';

/** O mês pode não ter nenhuma despesa ainda (não aberto), ter despesas sem fechamento (aberto) ou estar fechado. */
export type DashboardMonthState = 'not-open' | 'open' | 'closed';

export interface UnitSituation {
  readonly status: ChargeStatus;
  readonly label: string;
  readonly count: number;
  readonly icon: string;
  readonly tone: string;
}

export interface ExpenseAttention {
  readonly id: 'needs-amount' | 'overdue';
  readonly text: string;
}

const SITUATIONS: readonly { status: ChargeStatus; label: string }[] = [
  { status: 'paid', label: 'Pagas' },
  { status: 'open', label: 'Em aberto' },
  { status: 'partial', label: 'Parciais' },
  { status: 'overdue', label: 'Atrasadas' },
];

export function dashboardMonthState(data: MonthDashboard): DashboardMonthState {
  if (data.closed) {
    return 'closed';
  }
  return data.expenses.groups.length === 0 ? 'not-open' : 'open';
}

/** A parte do grupo no total do mês, em porcentagem inteira (arredondada, no máximo 100). */
export function groupShare(total: number, groupTotal: number): number {
  if (total <= 0 || groupTotal <= 0) {
    return 0;
  }
  return Math.min(100, Math.round((groupTotal / total) * 100));
}

/** Quantas unidades há em cada situação, na ordem em que a tela mostra; ícone e cor são os da tela de pagamentos. */
export function unitSituations(charges: DashboardCharges): UnitSituation[] {
  return SITUATIONS.map(({ status, label }) => {
    const view = chargeStatusView(status);
    return { status, label, count: charges[status], icon: view.icon, tone: view.tone };
  });
}

/** Avisos sobre as despesas do mês; o que falta preencher vem primeiro. */
export function expenseAttentions(expenses: DashboardExpenses): ExpenseAttention[] {
  const items: ExpenseAttention[] = [];
  if (expenses.needsAmountCount > 0) {
    items.push({
      id: 'needs-amount',
      text:
        expenses.needsAmountCount === 1
          ? '1 lançamento aguarda o valor'
          : `${String(expenses.needsAmountCount)} lançamentos aguardam o valor`,
    });
  }
  if (expenses.overdueCount > 0) {
    items.push({
      id: 'overdue',
      text:
        expenses.overdueCount === 1
          ? '1 despesa vencida'
          : `${String(expenses.overdueCount)} despesas vencidas`,
    });
  }
  return items;
}

/** Quantas cobranças do mês estão atrasadas; sem nenhuma, diz que não há. */
export function overdueChargesText(overdue: number): string {
  return overdue === 0 ? 'Nenhuma cobrança em atraso' : countOverdueCharges(overdue);
}
