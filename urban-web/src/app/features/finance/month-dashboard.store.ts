import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { MonthDashboardApi } from '../../core/finance/month-dashboard.api';
import { MonthDashboard } from '../../core/finance/month-dashboard.models';

/**
 * - loading: busca do mês (a primeira, a de outro mês ou uma nova tentativa);
 * - ready: o painel do mês na tela;
 * - error: a busca falhou; a tela oferece tentar de novo.
 */
export type MonthDashboardStatus = 'loading' | 'ready' | 'error';

interface MonthDashboardState {
  status: MonthDashboardStatus;
  month: string;
  data: MonthDashboard | null;
}

/** Estado do painel financeiro do mês. Fornecido no componente, então cada visita começa do zero. */
export const MonthDashboardStore = signalStore(
  withState<MonthDashboardState>({ status: 'loading', month: '', data: null }),
  withMethods((store) => {
    const api = inject(MonthDashboardApi);

    return {
      /** Lê o painel do mês. Resposta (ou falha) de um mês que a pessoa já deixou para trás é ignorada. */
      async show(month: string): Promise<void> {
        patchState(store, { status: 'loading', month });
        try {
          const data = await firstValueFrom(api.get(month));
          if (store.month() === month) {
            patchState(store, { status: 'ready', data });
          }
        } catch {
          if (store.month() === month) {
            patchState(store, { status: 'error' });
          }
        }
      },
    };
  }),
);
