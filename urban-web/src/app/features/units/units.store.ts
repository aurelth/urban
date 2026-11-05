import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { UnitsApi } from '../../core/units/units.api';
import {
  ApportionmentMethod,
  DEFAULT_UNITS_QUERY,
  Unit,
  UnitPayload,
  UnitsQuery,
  UnitsSummary,
} from '../../core/units/units.models';

/**
 * - loading: primeira busca (ou nova tentativa depois de um erro);
 * - ready: lista e resumo na tela;
 * - error: a busca falhou; a tela oferece tentar de novo.
 */
export type UnitsStatus = 'loading' | 'ready' | 'error';

export const CONCURRENCY_CONFLICT = 'concurrency.conflict';
const NOT_FOUND_STATUS = 404;

/** Recusas que significam "a tela está desatualizada": a lista é buscada de novo. */
const STALE_CODES: ReadonlySet<string> = new Set([
  CONCURRENCY_CONFLICT,
  'unit.deactivated',
  'unit.already_deactivated',
  'unit.already_active',
]);

type Filters = Partial<Pick<UnitsQuery, 'status' | 'type' | 'block' | 'search'>>;

interface UnitsState {
  status: UnitsStatus;
  query: UnitsQuery;
  items: readonly Unit[];
  total: number;
  summary: UnitsSummary | null;
  /** Há uma chamada de gravação ou de busca em andamento: a tela trava as ações. */
  busy: boolean;
}

/** Estado da lista de unidades. Fornecido no componente, então cada visita começa do zero. */
export const UnitsStore = signalStore(
  withState<UnitsState>({
    status: 'loading',
    query: DEFAULT_UNITS_QUERY,
    items: [],
    total: 0,
    summary: null,
    busy: false,
  }),
  withComputed((store) => ({
    pageCount: computed(() => Math.max(1, Math.ceil(store.total() / store.query().pageSize))),
    hasFilters: computed(() => {
      const query = store.query();
      return (
        query.status !== DEFAULT_UNITS_QUERY.status ||
        query.type !== null ||
        query.block.trim() !== '' ||
        query.search.trim() !== ''
      );
    }),
  })),
  withMethods((store) => {
    const api = inject(UnitsApi);

    const fetchList = async (query: UnitsQuery): Promise<void> => {
      const page = await firstValueFrom(api.list(query));
      patchState(store, { query, items: page.items, total: page.total });
    };

    const fetchSummary = async (): Promise<void> => {
      patchState(store, { summary: await firstValueFrom(api.summary()) });
    };

    /** Busca a lista e o resumo de novo. Se a página ficou vazia, volta uma página. */
    const refresh = async (): Promise<void> => {
      try {
        await Promise.all([fetchList(store.query()), fetchSummary()]);
        const { page, pageSize } = store.query();
        if (store.items().length === 0 && page > 1) {
          const lastPage = Math.max(1, Math.ceil(store.total() / pageSize));
          await fetchList({ ...store.query(), page: Math.min(page - 1, lastPage) });
        }
      } catch {
        patchState(store, { status: 'error' });
      }
    };

    const loadList = async (query: UnitsQuery): Promise<void> => {
      patchState(store, { busy: true });
      try {
        await fetchList(query);
      } catch {
        patchState(store, { status: 'error' });
      }
      patchState(store, { busy: false });
    };

    /** Faz uma gravação; se deu certo, atualiza a tela. Devolve o problema (ou nulo) para a tela explicar. */
    const act = async (request: Observable<unknown>): Promise<ApiProblem | null> => {
      patchState(store, { busy: true });
      let problem: ApiProblem | null = null;
      try {
        await firstValueFrom(request);
      } catch (error) {
        problem = toApiProblem(error);
      }
      if (problem === null || isStale(problem)) {
        await refresh();
      }
      patchState(store, { busy: false });
      return problem;
    };

    return {
      /** Primeira busca da lista e do resumo. Serve também para tentar de novo depois de um erro. */
      load: async (): Promise<void> => {
        patchState(store, { status: 'loading' });
        try {
          const [page, summary] = await Promise.all([
            firstValueFrom(api.list(store.query())),
            firstValueFrom(api.summary()),
          ]);
          patchState(store, { status: 'ready', items: page.items, total: page.total, summary });
        } catch {
          patchState(store, { status: 'error' });
        }
      },
      /** Muda os filtros e volta para a primeira página. O resumo não muda com filtro. */
      filter: (filters: Filters): Promise<void> =>
        loadList({ ...store.query(), ...filters, page: 1 }),
      goToPage: async (page: number): Promise<void> => {
        if (Number.isInteger(page) && page >= 1 && page <= store.pageCount()) {
          await loadList({ ...store.query(), page });
        }
      },
      create: (payload: UnitPayload): Promise<ApiProblem | null> => act(api.create(payload)),
      update: (target: Unit, payload: UnitPayload): Promise<ApiProblem | null> =>
        act(api.update(target.id, { ...payload, version: target.version })),
      deactivate: (target: Unit): Promise<ApiProblem | null> =>
        act(api.deactivate(target.id, target.version)),
      reactivate: (target: Unit): Promise<ApiProblem | null> =>
        act(api.reactivate(target.id, target.version)),
      changeMethod: async (method: ApportionmentMethod): Promise<ApiProblem | null> => {
        const summary = store.summary();
        if (summary === null) {
          return null;
        }
        patchState(store, { busy: true });
        let problem: ApiProblem | null = null;
        try {
          const changed = await firstValueFrom(
            api.changeMethod(summary.apportionmentVersion, method),
          );
          patchState(store, {
            summary: {
              ...summary,
              apportionmentMethod: changed.method,
              apportionmentVersion: changed.version,
            },
          });
        } catch (error) {
          problem = toApiProblem(error);
          if (problem.code === CONCURRENCY_CONFLICT) {
            await fetchSummary().catch(() => undefined);
          }
        }
        patchState(store, { busy: false });
        return problem;
      },
    };
  }),
);

function isStale(problem: ApiProblem): boolean {
  return (
    problem.status === NOT_FOUND_STATUS || (problem.code !== null && STALE_CODES.has(problem.code))
  );
}
