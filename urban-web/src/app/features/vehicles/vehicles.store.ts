import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { Observable, firstValueFrom } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { fetchActiveUnits } from '../../core/units/active-units';
import { UnitsApi } from '../../core/units/units.api';
import { Unit } from '../../core/units/units.models';
import { VehiclesApi } from '../../core/vehicles/vehicles.api';
import {
  DEFAULT_VEHICLES_QUERY,
  Vehicle,
  VehiclePayload,
  VehiclesQuery,
} from '../../core/vehicles/vehicles.models';

/**
 * - loading: primeira busca (ou nova tentativa depois de um erro);
 * - ready: lista e unidades na tela;
 * - error: a busca falhou; a tela oferece tentar de novo.
 */
export type VehiclesStatus = 'loading' | 'ready' | 'error';

export const CONCURRENCY_CONFLICT = 'concurrency.conflict';
const NOT_FOUND_STATUS = 404;

/** Recusas que significam "as unidades estão desatualizadas": a lista de unidades ativas é buscada de novo, junto com a de veículos. */
const STALE_UNIT_CODES: ReadonlySet<string> = new Set(['unit.not_found', 'unit.deactivated']);

type Filters = Partial<Pick<VehiclesQuery, 'unitId' | 'search'>>;

interface VehiclesState {
  status: VehiclesStatus;
  query: VehiclesQuery;
  items: readonly Vehicle[];
  total: number;
  /** As unidades ativas: servem ao filtro da lista e à escolha da unidade no diálogo. */
  units: readonly Unit[];
  /** Há uma chamada de gravação ou de busca em andamento: a tela trava as ações. */
  busy: boolean;
}

/** Estado da lista de veículos. Fornecido no componente, então cada visita começa do zero. */
export const VehiclesStore = signalStore(
  withState<VehiclesState>({
    status: 'loading',
    query: DEFAULT_VEHICLES_QUERY,
    items: [],
    total: 0,
    units: [],
    busy: false,
  }),
  withComputed((store) => ({
    pageCount: computed(() => Math.max(1, Math.ceil(store.total() / store.query().pageSize))),
    hasFilters: computed(() => {
      const query = store.query();
      return query.unitId !== null || query.search.trim() !== '';
    }),
  })),
  withMethods((store) => {
    const vehicles = inject(VehiclesApi);
    const unitsApi = inject(UnitsApi);

    const fetchList = async (query: VehiclesQuery): Promise<void> => {
      const page = await firstValueFrom(vehicles.list(query));
      patchState(store, { query, items: page.items, total: page.total });
    };

    /** Busca a lista de novo. Se a página ficou vazia, volta uma página. */
    const refreshList = async (): Promise<void> => {
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

    const refreshUnits = async (): Promise<void> => {
      try {
        patchState(store, { units: await fetchActiveUnits(unitsApi) });
      } catch {
        patchState(store, { status: 'error' });
      }
    };

    const loadList = async (query: VehiclesQuery): Promise<void> => {
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
      const staleUnits = problem !== null && isStaleUnits(problem);
      if (problem === null || staleUnits || isStaleList(problem)) {
        await refreshList();
      }
      if (staleUnits) {
        await refreshUnits();
      }
      patchState(store, { busy: false });
      return problem;
    };

    return {
      /** Primeira busca da lista e das unidades. Serve também para tentar de novo depois de um erro. */
      load: async (): Promise<void> => {
        patchState(store, { status: 'loading' });
        try {
          const [page, units] = await Promise.all([
            firstValueFrom(vehicles.list(store.query())),
            fetchActiveUnits(unitsApi),
          ]);
          patchState(store, { status: 'ready', items: page.items, total: page.total, units });
        } catch {
          patchState(store, { status: 'error' });
        }
      },
      /** Muda os filtros e volta para a primeira página. As unidades não mudam com filtro. */
      filter: (filters: Filters): Promise<void> =>
        loadList({ ...store.query(), ...filters, page: 1 }),
      goToPage: async (page: number): Promise<void> => {
        if (Number.isInteger(page) && page >= 1 && page <= store.pageCount()) {
          await loadList({ ...store.query(), page });
        }
      },
      create: (payload: VehiclePayload): Promise<ApiProblem | null> =>
        act(vehicles.create(payload)),
      update: (target: Vehicle, payload: VehiclePayload): Promise<ApiProblem | null> =>
        act(vehicles.update(target.id, { ...payload, version: target.version })),
      remove: (target: Vehicle): Promise<ApiProblem | null> => act(vehicles.delete(target.id)),
    };
  }),
);

/** Veículo que sumiu (404) ou foi alterado em outro lugar: a lista está desatualizada. */
function isStaleList(problem: ApiProblem): boolean {
  return problem.status === NOT_FOUND_STATUS || problem.code === CONCURRENCY_CONFLICT;
}

function isStaleUnits(problem: ApiProblem): boolean {
  return problem.code !== null && STALE_UNIT_CODES.has(problem.code);
}
