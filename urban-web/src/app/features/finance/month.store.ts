import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { Observable, firstValueFrom } from 'rxjs';
import { FinanceApi } from '../../core/finance/finance.api';
import {
  ExpenseEntry,
  GroupTotal,
  MonthEntries,
  SettableEntryStatus,
} from '../../core/finance/finance.models';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { CONCURRENCY_CONFLICT, FinanceStatus } from './expenses.store';

const NOT_FOUND_STATUS = 404;
const CONFLICT_STATUS = 409;
const OPEN_RETRIES = 1;

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
    closing: computed(() => store.data()?.closing ?? null),
    sections: computed<readonly GroupSection[]>(() => {
      const data = store.data();
      return (data?.groups ?? []).map((group) => ({
        group,
        entries: (data?.entries ?? []).filter((entry) => entry.groupId === group.groupId),
      }));
    }),
  })),
  withComputed((store) => ({
    isClosed: computed(() => store.closing() !== null),
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

    /**
     * Abre o mês: a API gera o que falta e devolve os lançamentos (repetir é seguro). Se duas aberturas se cruzarem, a
     * segunda recebe um conflito e tenta mais uma vez, já encontrando o que a primeira gerou.
     */
    const open = async (month: string): Promise<void> => {
      for (let attempt = 0; attempt <= OPEN_RETRIES; attempt++) {
        try {
          patchState(store, { data: await firstValueFrom(api.openMonth(month)), status: 'ready' });
          return;
        } catch (error) {
          if (toApiProblem(error).code !== CONCURRENCY_CONFLICT || attempt === OPEN_RETRIES) {
            patchState(store, { status: 'error' });
            return;
          }
        }
      }
    };

    /** Grava, e relê o mês quando deu certo ou quando a tela estava desatualizada, para refazer totais e vencidos. */
    const act = async (request: Observable<unknown>): Promise<ApiProblem | null> => {
      patchState(store, { busy: true });
      let problem: ApiProblem | null = null;
      try {
        await firstValueFrom(request);
      } catch (error) {
        problem = toApiProblem(error);
      }
      if (problem === null || isStale(problem)) {
        await read(store.month());
      }
      patchState(store, { busy: false });
      return problem;
    };

    /**
     * Fecha ou reabre o mês: a API devolve o mês já no novo estado, que substitui o da tela. Se a tela estava
     * desatualizada (o mês já estava fechado, ou já tinha sido reaberto, em outro lugar), relê o mês.
     */
    const change = async (request: Observable<MonthEntries>): Promise<ApiProblem | null> => {
      patchState(store, { busy: true });
      let problem: ApiProblem | null = null;
      try {
        patchState(store, { data: await firstValueFrom(request) });
      } catch (error) {
        problem = toApiProblem(error);
      }
      if (problem !== null && (isStale(problem) || problem.status === CONFLICT_STATUS)) {
        await read(store.month());
      }
      patchState(store, { busy: false });
      return problem;
    };

    return {
      /**
       * Mostra o mês: abre (gera os lançamentos que faltam) e lê. Não há botão para isso; quem entra na tela, ou troca de
       * mês, já vê os lançamentos. Serve também para tentar de novo depois de um erro.
       */
      show: async (month: string): Promise<void> => {
        patchState(store, { status: 'loading', month });
        await open(month);
      },
      close: (dueDate: string): Promise<ApiProblem | null> =>
        change(api.closeMonth(store.month(), dueDate)),
      reopen: (): Promise<ApiProblem | null> => change(api.reopenMonth(store.month())),
      setAmount: (target: ExpenseEntry, amount: number): Promise<ApiProblem | null> =>
        act(api.setEntryAmount(target.id, target.version, amount)),
      setStatus: (target: ExpenseEntry, status: SettableEntryStatus): Promise<ApiProblem | null> =>
        act(api.setEntryStatus(target.id, target.version, status)),
    };
  }),
);

function isStale(problem: ApiProblem): boolean {
  return problem.status === NOT_FOUND_STATUS || problem.code === CONCURRENCY_CONFLICT;
}
