import { signalStore, withMethods, withState } from '@ngrx/signals';
import { MonthDashboard } from '../../core/finance/month-dashboard.models';

export type MonthDashboardStatus = 'loading' | 'ready' | 'error';

interface MonthDashboardState {
  status: MonthDashboardStatus;
  month: string;
  data: MonthDashboard | null;
}

export const MonthDashboardStore = signalStore(
  withState<MonthDashboardState>({ status: 'loading', month: '', data: null }),
  withMethods(() => ({
    // eslint-disable-next-line @typescript-eslint/no-unused-vars -- esqueleto
    show: (_month: string): Promise<void> => Promise.resolve(),
  })),
);
