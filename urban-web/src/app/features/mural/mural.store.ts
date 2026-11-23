import { DestroyRef, computed, effect, inject } from '@angular/core';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { AnnouncementsApi } from '../../core/announcements/announcements.api';
import {
  AnnouncementCategory,
  MURAL_PAGE_SIZE,
  MyAnnouncement,
  MyAnnouncementPage,
  MyAnnouncementsQuery,
  UnreadByCategory,
} from '../../core/announcements/announcements.models';
import { MuralLive, MuralUpdate } from '../../core/announcements/mural-live';
import { NOW } from '../../core/finance/clock';
import { toApiProblem } from '../../core/http/api-problem';
import { CATEGORY_OPTIONS } from '../comunicados/announcement-values';
import { markedNotice } from './mural-values';

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
  /** O servidor avisou que o mural mudou e a lista na tela ainda é a de antes: `new` se há avisos novos, `changed` se só mudou o que havia. */
  pending: 'new' | 'changed' | null;
}

const NOT_FOUND_STATUS = 404;
const MESSAGES = {
  loadMore: 'Não foi possível carregar mais avisos. Tente de novo.',
  markRead: 'Não foi possível marcar o aviso como lido. Tente de novo.',
  markAll: 'Não foi possível marcar os avisos como lidos. Tente de novo.',
  gone: 'Este aviso não está mais disponível.',
};

/** Um não lido a mais (+1) ou a menos (-1), no total e na categoria, sem passar de zero. */
function shiftUnread(
  state: Pick<MuralState, 'unread' | 'unreadByCategory'>,
  category: AnnouncementCategory,
  delta: 1 | -1,
): Pick<MuralState, 'unread' | 'unreadByCategory'> {
  return {
    unread: Math.max(0, state.unread + delta),
    unreadByCategory: {
      ...state.unreadByCategory,
      [category]: Math.max(0, state.unreadByCategory[category] + delta),
    },
  };
}

function counters(
  result: MyAnnouncementPage,
): Pick<MuralState, 'total' | 'unread' | 'unreadByCategory'> {
  return { total: result.total, unread: result.unread, unreadByCategory: result.unreadByCategory };
}

type Pending = MuralState['pending'];

/** Avisos novos pesam mais que mudanças: quem já sabe que há aviso novo não volta a "só mudou". */
function strongest(current: Pending, incoming: 'new' | 'changed'): 'new' | 'changed' {
  return current === 'new' || incoming === 'new' ? 'new' : 'changed';
}

/**
 * O servidor avisou que o mural mudou. A lista na tela não se mexe (quem está lendo não perde o lugar): as contagens dos filtros
 * e do resumo andam na hora e a faixa pede para atualizar. Com o mural ainda carregando, ou em erro, não há o que avisar: a
 * busca que vier já traz o que há.
 */
function applyLiveUpdate(
  store: {
    status(): MuralStatus;
    unread(): number;
    unreadByCategory(): UnreadByCategory;
    pending(): Pending;
  } & Parameters<typeof patchState>[0],
  update: MuralUpdate,
): void {
  if (store.status() !== 'ready') {
    return;
  }
  const { counters } = update;
  // Uma reconexão sem nada diferente nas contagens não é novidade: a faixa só aparece se algo mudou no meio.
  const nothingChanged =
    update.resync &&
    counters !== null &&
    counters.unread === store.unread() &&
    (Object.keys(counters.unreadByCategory) as AnnouncementCategory[]).every(
      (category) => counters.unreadByCategory[category] === store.unreadByCategory()[category],
    );
  if (nothingChanged) {
    return;
  }
  patchState(store, {
    pending: strongest(store.pending(), update.kind),
    ...(counters === null
      ? {}
      : { unread: counters.unread, unreadByCategory: counters.unreadByCategory }),
  });
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
    pending: null,
  }),
  withComputed((store) => ({
    hasMore: computed(() => store.page() * MURAL_PAGE_SIZE < store.total()),
    hasFilters: computed(() => store.category() !== null || store.unreadOnly()),
    /** Quantos não lidos há no que o botão "Marcar como lidos" alcança: a categoria escolhida ou o mural inteiro. */
    unreadInScope: computed(() => {
      const category = store.category();
      return category === null ? store.unread() : store.unreadByCategory()[category];
    }),
    chips: computed((): readonly MuralChip[] => [
      { value: null, label: 'Todos', unread: store.unread() },
      ...CATEGORY_OPTIONS.map((option) => ({
        value: option.value,
        label: option.label,
        unread: store.unreadByCategory()[option.value],
      })),
    ]),
  })),
  withMethods((store) => {
    const api = inject(AnnouncementsApi);
    const now = inject(NOW);
    /** Conta as buscas da primeira página: a resposta de uma que já foi superada por outra é descartada. */
    let latest = 0;

    const query = (page: number): MyAnnouncementsQuery => ({
      page,
      pageSize: MURAL_PAGE_SIZE,
      unreadOnly: store.unreadOnly(),
      category: store.category(),
    });

    const clearMessages = (): void => {
      patchState(store, { notice: null, actionError: null });
    };

    const fetchFirst = async (showLoading: boolean): Promise<void> => {
      latest += 1;
      const mine = latest;
      // A busca traz o que há: o pedido de atualizar vale de novo só se outro aviso chegar durante ela.
      patchState(store, showLoading ? { status: 'loading' as const } : { refreshing: true }, {
        loadingMore: false,
        pending: null,
      });
      try {
        const result = await firstValueFrom(api.listMine(query(1)));
        if (mine === latest) {
          patchState(store, {
            status: 'ready',
            refreshing: false,
            items: result.items,
            page: 1,
            ...counters(result),
          });
        }
      } catch {
        if (mine === latest) {
          patchState(store, { status: 'error', refreshing: false });
        }
      }
    };

    const changeFilters = async (filters: Partial<MuralState>): Promise<void> => {
      clearMessages();
      patchState(store, filters);
      await fetchFirst(false);
    };

    const setRead = (id: string, read: boolean): readonly MyAnnouncement[] =>
      store
        .items()
        .map((item) =>
          item.id === id ? { ...item, read, readAt: read ? now().toISOString() : null } : item,
        );

    const unreadNow = (): Pick<MuralState, 'unread' | 'unreadByCategory'> => ({
      unread: store.unread(),
      unreadByCategory: store.unreadByCategory(),
    });

    const stopMarking = (id: string): void => {
      patchState(store, { marking: store.marking().filter((candidate) => candidate !== id) });
    };

    return {
      /** Primeira busca. Serve também para tentar de novo depois de um erro, com os filtros de agora. */
      load: (): Promise<void> => fetchFirst(true),
      refresh: async (): Promise<void> => {
        clearMessages();
        await fetchFirst(false);
      },
      setCategory: async (category: AnnouncementCategory | null): Promise<void> => {
        if (store.category() !== category) {
          await changeFilters({ category });
        }
      },
      setUnreadOnly: async (unreadOnly: boolean): Promise<void> => {
        if (store.unreadOnly() !== unreadOnly) {
          await changeFilters({ unreadOnly });
        }
      },
      clearFilters: async (): Promise<void> => {
        if (store.hasFilters()) {
          await changeFilters({ category: null, unreadOnly: false });
        }
      },
      loadMore: async (): Promise<void> => {
        if (!store.hasMore() || store.loadingMore() || store.status() !== 'ready') {
          return;
        }
        const mine = latest;
        const page = store.page() + 1;
        patchState(store, { loadingMore: true, actionError: null });
        try {
          const result = await firstValueFrom(api.listMine(query(page)));
          if (mine === latest) {
            const known = new Set(store.items().map((item) => item.id));
            patchState(store, {
              items: [...store.items(), ...result.items.filter((item) => !known.has(item.id))],
              page,
              loadingMore: false,
              ...counters(result),
            });
          }
        } catch {
          if (mine === latest) {
            patchState(store, { loadingMore: false, actionError: MESSAGES.loadMore });
          }
        }
      },
      /** Marca na tela antes de a API responder; se ela recusar, desfaz e avisa. */
      markRead: async (item: MyAnnouncement): Promise<void> => {
        if (item.read || store.marking().includes(item.id)) {
          return;
        }
        clearMessages();
        patchState(store, {
          items: setRead(item.id, true),
          marking: [...store.marking(), item.id],
          ...shiftUnread(unreadNow(), item.category, -1),
        });
        try {
          await firstValueFrom(api.markRead(item.id));
          stopMarking(item.id);
        } catch (error) {
          stopMarking(item.id);
          if (toApiProblem(error).status === NOT_FOUND_STATUS) {
            patchState(store, { actionError: MESSAGES.gone });
            await fetchFirst(false);
            return;
          }
          patchState(store, {
            items: setRead(item.id, false),
            actionError: MESSAGES.markRead,
            ...shiftUnread(unreadNow(), item.category, 1),
          });
        }
      },
      /** Marca como lidos os avisos da categoria escolhida (ou todos) e busca o mural de novo. */
      markAll: async (): Promise<void> => {
        if (store.markingAll()) {
          return;
        }
        clearMessages();
        patchState(store, { markingAll: true });
        try {
          const result = await firstValueFrom(api.markAllRead(store.category()));
          await fetchFirst(false);
          patchState(store, { notice: markedNotice(result.marked) });
        } catch {
          patchState(store, { actionError: MESSAGES.markAll });
        }
        patchState(store, { markingAll: false });
      },
      dismissNotice: (): void => {
        patchState(store, { notice: null });
      },
    };
  }),
  withHooks((store) => ({
    onInit: (): void => {
      const live = inject(MuralLive);
      const subscription = live.updates.subscribe((update) => {
        applyLiveUpdate(store, update);
      });
      inject(DestroyRef).onDestroy(() => {
        subscription.unsubscribe();
      });
      // A bolinha do menu segue o mural, mas só depois de ele ter lido a contagem: antes disso ela zeraria a que o serviço já tinha.
      effect(() => {
        if (store.status() === 'ready') {
          live.setUnread(store.unread());
        }
      });
    },
  })),
);
