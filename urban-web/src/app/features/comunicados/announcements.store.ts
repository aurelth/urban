import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { Observable, firstValueFrom } from 'rxjs';
import { AnnouncementsApi } from '../../core/announcements/announcements.api';
import {
  ANNOUNCEMENTS_PAGE_SIZE,
  Announcement,
  AnnouncementPayload,
} from '../../core/announcements/announcements.models';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { fetchActiveUnits } from '../../core/units/active-units';
import { UnitsApi } from '../../core/units/units.api';
import { Unit } from '../../core/units/units.models';

/**
 * - loading: primeira busca (ou nova tentativa depois de um erro);
 * - ready: lista e unidades na tela;
 * - error: a busca falhou; a tela oferece tentar de novo.
 */
export type AnnouncementsStatus = 'loading' | 'ready' | 'error';

export const CONCURRENCY_CONFLICT = 'concurrency.conflict';
const NOT_FOUND_STATUS = 404;

/** Recusas que significam "a tela está desatualizada": a lista é buscada de novo. */
const STALE_CODES: ReadonlySet<string> = new Set([CONCURRENCY_CONFLICT, 'announcement.not_found']);

interface AnnouncementsState {
  status: AnnouncementsStatus;
  page: number;
  items: readonly Announcement[];
  total: number;
  /** As unidades ativas: dão o nome da unidade na lista e as escolhas do diálogo. */
  units: readonly Unit[];
  /** Há uma chamada de gravação ou de busca em andamento: a tela trava as ações. */
  busy: boolean;
}

/** Estado da lista de comunicados do síndico. Fornecido no componente, então cada visita começa do zero. */
export const AnnouncementsStore = signalStore(
  withState<AnnouncementsState>({
    status: 'loading',
    page: 1,
    items: [],
    total: 0,
    units: [],
    busy: false,
  }),
  withComputed((store) => ({
    pageCount: computed(() => Math.max(1, Math.ceil(store.total() / ANNOUNCEMENTS_PAGE_SIZE))),
    /** Os blocos que têm unidade ativa, sem repetir e em ordem. */
    blocks: computed(() =>
      [...new Set(store.units().flatMap((unit) => (unit.block === null ? [] : [unit.block])))].sort(
        (first, second) => first.localeCompare(second, 'pt-BR', { numeric: true }),
      ),
    ),
  })),
  withMethods((store) => {
    const api = inject(AnnouncementsApi);
    const unitsApi = inject(UnitsApi);

    const fetchList = async (page: number): Promise<void> => {
      const result = await firstValueFrom(api.list(page, ANNOUNCEMENTS_PAGE_SIZE));
      patchState(store, { page, items: result.items, total: result.total });
    };

    /** Busca a página de novo. Se ela ficou vazia, volta uma página. */
    const refresh = async (page: number): Promise<void> => {
      try {
        await fetchList(page);
        if (store.items().length === 0 && page > 1) {
          await fetchList(Math.min(page - 1, store.pageCount()));
        }
      } catch {
        patchState(store, { status: 'error' });
      }
    };

    /** Faz uma gravação; se deu certo, atualiza a tela. Devolve o problema (ou nulo) para a tela explicar. */
    const act = async (
      request: Observable<unknown>,
      pageAfter: number,
    ): Promise<ApiProblem | null> => {
      patchState(store, { busy: true });
      let problem: ApiProblem | null = null;
      try {
        await firstValueFrom(request);
      } catch (error) {
        problem = toApiProblem(error);
      }
      if (problem === null || isStale(problem)) {
        await refresh(pageAfter);
      }
      patchState(store, { busy: false });
      return problem;
    };

    return {
      /** Primeira busca da lista e das unidades. Serve também para tentar de novo depois de um erro. */
      load: async (): Promise<void> => {
        patchState(store, { status: 'loading' });
        try {
          const [result, units] = await Promise.all([
            firstValueFrom(api.list(store.page(), ANNOUNCEMENTS_PAGE_SIZE)),
            fetchActiveUnits(unitsApi),
          ]);
          patchState(store, { status: 'ready', items: result.items, total: result.total, units });
        } catch {
          patchState(store, { status: 'error' });
        }
      },
      goToPage: async (page: number): Promise<void> => {
        if (!Number.isInteger(page) || page < 1 || page > store.pageCount()) {
          return;
        }
        patchState(store, { busy: true });
        try {
          await fetchList(page);
        } catch {
          patchState(store, { status: 'error' });
        }
        patchState(store, { busy: false });
      },
      /** O comunicado novo é o mais recente: depois de criar, a lista volta para a primeira página. */
      create: (payload: AnnouncementPayload): Promise<ApiProblem | null> =>
        act(api.create(payload), 1),
      update: (target: Announcement, payload: AnnouncementPayload): Promise<ApiProblem | null> =>
        act(api.update(target.id, { ...payload, version: target.version }), store.page()),
      remove: (target: Announcement): Promise<ApiProblem | null> =>
        act(api.remove(target.id, target.version), store.page()),
    };
  }),
);

function isStale(problem: ApiProblem): boolean {
  return (
    problem.status === NOT_FOUND_STATUS || (problem.code !== null && STALE_CODES.has(problem.code))
  );
}
