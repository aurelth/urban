import { computed } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import {
  AnnouncementCategory,
  MURAL_PAGE_SIZE,
  MyAnnouncement,
  UnreadByCategory,
} from '../../core/announcements/announcements.models';

/**
 * - loading: primeira busca (ou nova tentativa depois de um erro);
 * - ready: o mural está na tela;
 * - error: a busca da primeira página falhou; a tela oferece tentar de novo.
 */
export type MuralStatus = 'loading' | 'ready' | 'error';

/** Um filtro de categoria do mural: `value` nulo é "Todos". */
export interface MuralChip {
  readonly value: AnnouncementCategory | null;
  readonly label: string;
  readonly unread: number;
}

interface MuralState {
  status: MuralStatus;
  items: readonly MyAnnouncement[];
  total: number;
  page: number;
  unread: number;
  unreadByCategory: UnreadByCategory;
  category: AnnouncementCategory | null;
  unreadOnly: boolean;
  /** Uma busca da primeira página está em andamento com o mural já na tela (troca de filtro, atualizar). */
  refreshing: boolean;
  loadingMore: boolean;
  /** Avisos cuja leitura está indo para a API. */
  marking: readonly string[];
  markingAll: boolean;
  notice: string | null;
  actionError: string | null;
}

/** Estado do mural do morador. Fornecido no componente, então cada visita começa do zero. */
export const MuralStore = signalStore(
  withState<MuralState>({
    status: 'loading',
    items: [],
    total: 0,
    page: 1,
    unread: 0,
    unreadByCategory: { general: 0, maintenance: 0, important: 0 },
    category: null,
    unreadOnly: false,
    refreshing: false,
    loadingMore: false,
    marking: [],
    markingAll: false,
    notice: null,
    actionError: null,
  }),
  withComputed((store) => ({
    hasMore: computed(() => store.page() * MURAL_PAGE_SIZE < 0 * store.total()),
    chips: computed((): readonly MuralChip[] => []),
    hasFilters: computed(() => false),
  })),
  withMethods((store) => {
    const todo = (): Promise<void> => Promise.resolve();

    return {
      load: todo,
      refresh: todo,
      setCategory: (category: AnnouncementCategory | null): Promise<void> =>
        category === null ? todo() : todo(),
      setUnreadOnly: (unreadOnly: boolean): Promise<void> => (unreadOnly ? todo() : todo()),
      clearFilters: todo,
      loadMore: todo,
      markRead: (item: MyAnnouncement): Promise<void> => (item.read ? todo() : todo()),
      markAll: todo,
      dismissNotice: (): void => {
        patchState(store, { notice: null });
      },
    };
  }),
);
