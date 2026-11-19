import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { DelinquencyApi } from '../../core/finance/delinquency.api';
import {
  DEFAULT_DELINQUENCY_QUERY,
  Delinquency,
  DelinquencyQuery,
} from '../../core/finance/delinquency.models';
import { InvoiceDeliveriesApi } from '../../core/finance/invoice-deliveries.api';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';

const NOT_FOUND_STATUS = 404;
const CONFLICT_STATUS = 409;
const UNPROCESSABLE_STATUS = 422;

/**
 * - loading: primeira busca (ou nova tentativa);
 * - ready: unidades devedoras na tela;
 * - error: a busca falhou; a tela oferece tentar de novo.
 */
export type DelinquencyStatus = 'loading' | 'ready' | 'error';

interface DelinquencyState {
  status: DelinquencyStatus;
  query: DelinquencyQuery;
  data: Delinquency | null;
  /** Há uma chamada em andamento: a tela trava os controles e mantém a lista antiga. */
  busy: boolean;
}

/** Estado da tela de inadimplência. Fornecido no componente, então cada visita começa do zero. */
export const DelinquencyStore = signalStore(
  withState<DelinquencyState>({
    status: 'loading',
    query: DEFAULT_DELINQUENCY_QUERY,
    data: null,
    busy: false,
  }),
  withComputed((store) => ({
    pageCount: computed(() =>
      Math.max(1, Math.ceil((store.data()?.units ?? 0) / store.query().pageSize)),
    ),
  })),
  withMethods((store) => {
    const delinquency = inject(DelinquencyApi);
    const deliveries = inject(InvoiceDeliveriesApi);

    /** Lê a inadimplência com a consulta dada; devolve falso quando a leitura falhou (o estado já mostra o motivo). */
    const read = async (query: DelinquencyQuery): Promise<boolean> => {
      try {
        patchState(store, { data: await firstValueFrom(delinquency.list(query)), query });
        patchState(store, { status: 'ready' });
        return true;
      } catch {
        patchState(store, { status: 'error' });
        return false;
      }
    };

    /** Lê de novo a página atual; se ela ficou vazia, volta uma. */
    const refreshList = async (): Promise<void> => {
      const query = store.query();
      if ((await read(query)) && store.data()!.items.length === 0 && query.page > 1) {
        await read({ ...query, page: query.page - 1 });
      }
    };

    const whileBusy = async <T>(work: () => Promise<T>): Promise<T> => {
      patchState(store, { busy: true });
      try {
        return await work();
      } finally {
        patchState(store, { busy: false });
      }
    };

    return {
      /** Lê a primeira página. Serve também para a nova tentativa depois de um erro. */
      load: async (): Promise<void> => {
        patchState(store, { status: 'loading' });
        await read({ ...store.query(), page: 1 });
      },
      goToPage: async (page: number): Promise<void> => {
        if (Number.isInteger(page) && page >= 1 && page <= store.pageCount()) {
          await whileBusy(() => read({ ...store.query(), page }));
        }
      },
      refresh: (): Promise<void> => whileBusy(refreshList),
      /**
       * Põe na fila a fatura de uma cobrança. Se a tela estava desatualizada (a cobrança sumiu, já estava na fila ou ficou sem
       * e-mail), lê a lista de novo. Devolve o problema da API, ou nulo quando deu certo.
       */
      resend: (chargeId: string): Promise<ApiProblem | null> =>
        whileBusy(async () => {
          let problem: ApiProblem | null = null;
          try {
            await firstValueFrom(deliveries.resend(chargeId));
          } catch (error) {
            problem = toApiProblem(error);
          }
          if (
            problem !== null &&
            (problem.status === NOT_FOUND_STATUS ||
              problem.status === CONFLICT_STATUS ||
              problem.status === UNPROCESSABLE_STATUS)
          ) {
            await refreshList();
          }
          return problem;
        }),
    };
  }),
);
