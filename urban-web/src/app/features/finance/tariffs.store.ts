import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { Observable, firstValueFrom } from 'rxjs';
import { FinanceApi } from '../../core/finance/finance.api';
import { ConsumptionTariff, ConsumptionTariffPayload } from '../../core/finance/finance.models';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { FinanceStatus } from './expenses.store';

const NOT_FOUND_STATUS = 404;

interface TariffsState {
  status: FinanceStatus;
  tariffs: readonly ConsumptionTariff[];
  reserveFundPercent: number;
  busy: boolean;
}

/** Estado da tela de tarifas de consumo. Fornecido no componente, então cada visita começa do zero. */
export const TariffsStore = signalStore(
  withState<TariffsState>({ status: 'loading', tariffs: [], reserveFundPercent: 0, busy: false }),
  withMethods((store) => {
    const api = inject(FinanceApi);

    const fetchAll = async (): Promise<void> => {
      const [tariffs, settings] = await Promise.all([
        firstValueFrom(api.listTariffs()),
        firstValueFrom(api.getSettings()),
      ]);
      patchState(store, { tariffs, reserveFundPercent: settings.reserveFundPercent });
    };

    const refresh = async (): Promise<void> => {
      try {
        await fetchAll();
      } catch {
        patchState(store, { status: 'error' });
      }
    };

    /** Faz uma gravação e atualiza a tela. Devolve o problema (ou nulo) para a tela explicar. */
    const act = async (request: Observable<unknown>): Promise<ApiProblem | null> => {
      patchState(store, { busy: true });
      let problem: ApiProblem | null = null;
      try {
        await firstValueFrom(request);
      } catch (error) {
        problem = toApiProblem(error);
      }
      if (problem === null || problem.status === NOT_FOUND_STATUS) {
        await refresh();
      }
      patchState(store, { busy: false });
      return problem;
    };

    return {
      /** Busca as tarifas e o percentual. Serve também para tentar de novo depois de um erro. */
      load: async (): Promise<void> => {
        patchState(store, { status: 'loading' });
        try {
          await fetchAll();
          patchState(store, { status: 'ready' });
        } catch {
          patchState(store, { status: 'error' });
        }
      },
      saveTariff: (month: string, payload: ConsumptionTariffPayload): Promise<ApiProblem | null> =>
        act(api.saveTariff(month, payload)),
      deleteTariff: (month: string): Promise<ApiProblem | null> => act(api.deleteTariff(month)),
      setReserveFundPercent: (percent: number): Promise<ApiProblem | null> =>
        act(api.setReserveFundPercent(percent)),
    };
  }),
);
