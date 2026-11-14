import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { ResidentsApi } from '../../core/residents/residents.api';
import {
  DEFAULT_RESIDENTS_QUERY,
  Resident,
  ResidentsQuery,
} from '../../core/residents/residents.models';

/**
 * - loading: primeira busca (ou nova tentativa depois de um erro);
 * - ready: lista na tela;
 * - error: a busca falhou; a tela oferece tentar de novo.
 */
export type ResidentsStatus = 'loading' | 'ready' | 'error';

type Filters = Partial<Pick<ResidentsQuery, 'status' | 'search'>>;

interface ResidentsState {
  status: ResidentsStatus;
  query: ResidentsQuery;
  items: readonly Resident[];
  total: number;
  /** Há uma busca em andamento: a tela trava os controles. */
  busy: boolean;
}

/** Estado da lista de moradores. Fornecido no componente, então cada visita começa do zero. */
export const ResidentsStore = signalStore(
  withState<ResidentsState>({
    status: 'loading',
    query: DEFAULT_RESIDENTS_QUERY,
    items: [],
    total: 0,
    busy: false,
  }),
  withComputed((store) => ({
    pageCount: computed(() => Math.max(1, Math.ceil(store.total() / store.query().pageSize))),
    hasFilters: computed(() => {
      const query = store.query();
      return query.status !== DEFAULT_RESIDENTS_QUERY.status || query.search.trim() !== '';
    }),
  })),
  withMethods((store) => {
    const api = inject(ResidentsApi);

    const loadList = async (query: ResidentsQuery): Promise<void> => {
      patchState(store, { busy: true });
      try {
        const page = await firstValueFrom(api.list(query));
        patchState(store, { query, items: page.items, total: page.total });
      } catch {
        patchState(store, { status: 'error' });
      }
      patchState(store, { busy: false });
    };

    return {
      /** Primeira busca da lista. Serve também para tentar de novo depois de um erro. */
      load: async (): Promise<void> => {
        patchState(store, { status: 'loading' });
        try {
          const page = await firstValueFrom(api.list(store.query()));
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
    };
  }),
);
