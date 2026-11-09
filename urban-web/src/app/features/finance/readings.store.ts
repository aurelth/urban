import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { FinanceApi } from '../../core/finance/finance.api';
import { MonthReadings, ReadingInput } from '../../core/finance/finance.models';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { FinanceStatus } from './expenses.store';

const NOT_FOUND_STATUS = 404;
const CONFLICT_STATUS = 409;

interface ReadingsState {
  status: FinanceStatus;
  month: string;
  data: MonthReadings | null;
  busy: boolean;
}

/** Estado da tela de leituras do mês. Fornecido no componente, então cada visita começa do zero. */
export const ReadingsStore = signalStore(
  withState<ReadingsState>({ status: 'loading', month: '', data: null, busy: false }),
  withMethods((store) => {
    const api = inject(FinanceApi);

    const read = async (month: string): Promise<void> => {
      try {
        patchState(store, { data: await firstValueFrom(api.getReadings(month)), status: 'ready' });
      } catch {
        patchState(store, { status: 'error' });
      }
    };

    return {
      /** Lê as leituras do mês. Serve também para trocar de mês e para tentar de novo depois de um erro. */
      show: async (month: string): Promise<void> => {
        patchState(store, { status: 'loading', month });
        await read(month);
      },
      /**
       * Grava as leituras (a API aceita o pedido inteiro ou recusa inteiro) e mostra o que ela devolve. Se a tela estava
       * desatualizada (o mês foi fechado, ou uma unidade saiu, em outro lugar), lê o mês de novo.
       */
      save: async (readings: readonly ReadingInput[]): Promise<ApiProblem | null> => {
        patchState(store, { busy: true });
        let problem: ApiProblem | null = null;
        try {
          patchState(store, {
            data: await firstValueFrom(api.saveReadings(store.month(), readings)),
          });
        } catch (error) {
          problem = toApiProblem(error);
        }
        if (
          problem !== null &&
          (problem.status === NOT_FOUND_STATUS || problem.status === CONFLICT_STATUS)
        ) {
          await read(store.month());
        }
        patchState(store, { busy: false });
        return problem;
      },
    };
  }),
);
