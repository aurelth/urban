import {
  DashboardCharges,
  DashboardExpenses,
  MonthDashboard,
} from '../../core/finance/month-dashboard.models';
import { ChargeStatus } from '../../core/finance/payments.models';

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

/* eslint-disable @typescript-eslint/no-unused-vars -- esqueleto: a implementação vem no próximo commit */
export function dashboardMonthState(_data: MonthDashboard): DashboardMonthState {
  return 'not-open';
}

export function groupShare(_total: number, _groupTotal: number): number {
  return -1;
}

export function unitSituations(_charges: DashboardCharges): UnitSituation[] {
  return [];
}

export function expenseAttentions(_expenses: DashboardExpenses): ExpenseAttention[] {
  return [];
}

export function overdueChargesText(_overdue: number): string {
  return '';
}
