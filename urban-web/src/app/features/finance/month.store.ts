import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { FinanceApi } from '../../core/finance/finance.api';
import { ExpenseEntry, GroupTotal, MonthEntries } from '../../core/finance/finance.models';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { CONCURRENCY_CONFLICT, FinanceStatus } from './expenses.store';

const NOT_FOUND_STATUS = 404;

export interface GroupSection {
  readonly group: GroupTotal;
  readonly entries: readonly ExpenseEntry[];
}

interface MonthState {
  status: FinanceStatus;
  month: string;
  data: MonthEntries | null;
  busy: boolean;
}

/** Estado da tela de lançamentos do mês. Fornecido no componente, então cada visita começa do zero. */
export const MonthStore = signalStore(
  withState<MonthState>({ status: 'loading', month: '', data: null, busy: false }),
  withComputed((store) => ({
    sections: computed<readonly GroupSection[]>(() => {
      const data = store.data();
      return (data?.groups ?? []).map((group) => ({
        group,
        entries: (data?.entries ?? []).filter((entry) => entry.groupId === group.groupId),
      }));
    }),
  })),
  withMethods((store) => {
    const api = inject(FinanceApi);

    const read = async (month: string): Promise<void> => {
      try {
        patchState(store, { data: await firstValueFrom(api.getMonth(month)), status: 'ready' });
      } catch {
        patchState(store, { status: 'error' });
      }
    };

    return {
      /** Lê os lançamentos do mês (sem gerar nada). Serve também para tentar de novo depois de um erro. */
      show: async (month: string): Promise<void> => {
        patchState(store, { status: 'loading', month });
        await read(month);
      },
      open: async (): Promise<ApiProblem | null> => {
        patchState(store, { busy: true });
        let problem: ApiProblem | null = null;
        try {
          patchState(store, { data: await firstValueFrom(api.openMonth(store.month())) });
        } catch (error) {
          problem = toApiProblem(error);
        }
        patchState(store, { busy: false });
        return problem;
      },
      setAmount: async (target: ExpenseEntry, amount: number): Promise<ApiProblem | null> => {
        patchState(store, { busy: true });
        let problem: ApiProblem | null = null;
        try {
          await firstValueFrom(api.setEntryAmount(target.id, target.version, amount));
        } catch (error) {
          problem = toApiProblem(error);
        }
        if (problem === null || isStale(problem)) {
          await read(store.month());
        }
        patchState(store, { busy: false });
        return problem;
      },
    };
  }),
);

function isStale(problem: ApiProblem): boolean {
  return problem.status === NOT_FOUND_STATUS || problem.code === CONCURRENCY_CONFLICT;
}
