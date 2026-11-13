import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { Observable, firstValueFrom } from 'rxjs';
import { AdminApi } from '../../../core/admin/admin.api';
import { DEFAULT_MANAGERS_QUERY, Manager, ManagersQuery } from '../../../core/admin/admin.models';
import { ApiProblem, toApiProblem } from '../../../core/http/api-problem';

/**
 * - loading: primeira busca (ou nova tentativa depois de um erro);
 * - ready: a lista está na tela, vazia ou não;
 * - error: a busca falhou; a tela oferece tentar de novo.
 */
export type ManagersStatus = 'loading' | 'ready' | 'error';

/** Recusas que significam "a tela está desatualizada" (o síndico sumiu ou mudou de situação): a lista é buscada de novo. */
const STALE_STATUSES: ReadonlySet<number> = new Set([404, 409]);

type Filters = Partial<Pick<ManagersQuery, 'status' | 'search'>>;

interface ManagersState {
  status: ManagersStatus;
  query: ManagersQuery;
  items: readonly Manager[];
  total: number;
  /** Há uma busca ou uma alteração em andamento: a tela trava as ações. */
  busy: boolean;
}

/** Estado da lista de síndicos. Fornecido no componente, então cada visita começa do zero. */
export const ManagersStore = signalStore(
  withState<ManagersState>({
    status: 'loading',
    query: DEFAULT_MANAGERS_QUERY,
    items: [],
    total: 0,
    busy: false,
  }),
  withComputed((store) => ({
    pageCount: computed(() => Math.max(1, Math.ceil(store.total() / store.query().pageSize))),
    hasFilters: computed(() => {
      const query = store.query();
      return query.status !== DEFAULT_MANAGERS_QUERY.status || query.search.trim() !== '';
    }),
  })),
  withMethods((store) => {
    const api = inject(AdminApi);

    const fetchList = async (query: ManagersQuery): Promise<void> => {
      const page = await firstValueFrom(api.listManagers(query));
      patchState(store, { query, items: page.items, total: page.total });
    };

    /** Busca a lista de novo. Se a página ficou vazia, volta uma página. */
    const refresh = async (): Promise<void> => {
      try {
        await fetchList(store.query());
        const { page, pageSize } = store.query();
        if (store.items().length === 0 && page > 1) {
          const lastPage = Math.max(1, Math.ceil(store.total() / pageSize));
          await fetchList({ ...store.query(), page: Math.min(page - 1, lastPage) });
        }
      } catch {
        patchState(store, { status: 'error' });
      }
    };

    const loadList = async (query: ManagersQuery): Promise<void> => {
      patchState(store, { busy: true });
      try {
        await fetchList(query);
      } catch {
        patchState(store, { status: 'error' });
      }
      patchState(store, { busy: false });
    };

    /** Faz uma alteração; se deu certo (ou a tela estava desatualizada), busca a lista de novo. Devolve o problema, ou nulo. */
    const act = async (request: Observable<void>): Promise<ApiProblem | null> => {
      patchState(store, { busy: true });
      let problem: ApiProblem | null = null;
      try {
        await firstValueFrom(request);
      } catch (error) {
        problem = toApiProblem(error);
      }
      if (problem === null || STALE_STATUSES.has(problem.status)) {
        await refresh();
      }
      patchState(store, { busy: false });
      return problem;
    };

    return {
      /** Primeira busca. Serve também para tentar de novo depois de um erro. */
      load: async (): Promise<void> => {
        patchState(store, { status: 'loading' });
        try {
          const page = await firstValueFrom(api.listManagers(store.query()));
          patchState(store, { status: 'ready', items: page.items, total: page.total });
        } catch {
          patchState(store, { status: 'error' });
        }
      },
      /** Muda os filtros e volta para a primeira página. */
      filter: (filters: Filters): Promise<void> =>
        loadList({ ...store.query(), ...filters, page: 1 }),
      goToPage: async (page: number): Promise<void> => {
        if (Number.isInteger(page) && page >= 1 && page <= store.pageCount()) {
          await loadList({ ...store.query(), page });
        }
      },
      suspend: (target: Manager, reason: string): Promise<ApiProblem | null> =>
        act(api.suspend(target.id, reason)),
      reactivate: (target: Manager): Promise<ApiProblem | null> => act(api.reactivate(target.id)),
    };
  }),
);
