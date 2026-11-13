import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { Observable, firstValueFrom } from 'rxjs';
import { ContactMessagesApi } from '../../../core/admin/contact-messages.api';
import {
  ContactMessage,
  ContactMessagesFilter,
  ContactMessagesQuery,
  DEFAULT_CONTACT_MESSAGES_QUERY,
} from '../../../core/admin/contact-messages.models';
import { ApiProblem, toApiProblem } from '../../../core/http/api-problem';

/**
 * - loading: primeira busca (ou nova tentativa depois de um erro);
 * - ready: a lista está na tela, vazia ou não;
 * - error: a busca falhou; a tela oferece tentar de novo.
 */
export type ContactMessagesStatus = 'loading' | 'ready' | 'error';

/** Recusa que significa "a tela está desatualizada" (a mensagem sumiu): a lista é buscada de novo. */
const STALE_STATUS = 404;

interface ContactMessagesState {
  status: ContactMessagesStatus;
  query: ContactMessagesQuery;
  items: readonly ContactMessage[];
  total: number;
  /** Há uma busca ou uma alteração em andamento: a tela trava as ações. */
  busy: boolean;
}

/** Estado da caixa de mensagens. Fornecido no componente, então cada visita começa do zero. */
export const ContactMessagesStore = signalStore(
  withState<ContactMessagesState>({
    status: 'loading',
    query: DEFAULT_CONTACT_MESSAGES_QUERY,
    items: [],
    total: 0,
    busy: false,
  }),
  withComputed((store) => ({
    pageCount: computed(() => Math.max(1, Math.ceil(store.total() / store.query().pageSize))),
    hasFilters: computed(() => store.query().status !== DEFAULT_CONTACT_MESSAGES_QUERY.status),
  })),
  withMethods((store) => {
    const api = inject(ContactMessagesApi);

    const fetchList = async (query: ContactMessagesQuery): Promise<void> => {
      const page = await firstValueFrom(api.list(query));
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

    const loadList = async (query: ContactMessagesQuery): Promise<void> => {
      patchState(store, { busy: true });
      try {
        await fetchList(query);
      } catch {
        patchState(store, { status: 'error' });
      }
      patchState(store, { busy: false });
    };

    /** Faz a alteração e busca a lista de novo quando deu certo ou a tela estava desatualizada (404). */
    const change = async (call: Observable<unknown>): Promise<ApiProblem | null> => {
      patchState(store, { busy: true });
      let problem: ApiProblem | null = null;
      try {
        await firstValueFrom(call);
      } catch (error) {
        problem = toApiProblem(error);
      }
      if (problem === null || problem.status === STALE_STATUS) {
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
          const page = await firstValueFrom(api.list(store.query()));
          patchState(store, { status: 'ready', items: page.items, total: page.total });
        } catch {
          patchState(store, { status: 'error' });
        }
      },
      /** Muda o filtro e volta para a primeira página. */
      filter: (filters: { status: ContactMessagesFilter }): Promise<void> =>
        loadList({ ...store.query(), ...filters, page: 1 }),
      goToPage: async (page: number): Promise<void> => {
        if (Number.isInteger(page) && page >= 1 && page <= store.pageCount()) {
          await loadList({ ...store.query(), page });
        }
      },
      /** Marca como lida e busca a lista de novo (também quando a mensagem sumiu). Devolve o problema, ou nulo. */
      markRead: (message: ContactMessage): Promise<ApiProblem | null> =>
        change(api.markRead(message.id)),
      /**
       * Responde por e-mail e busca a lista de novo, que agora traz a resposta e a mensagem como lida. Se a API recusa, nada
       * foi enviado nem registrado: a lista fica como estava (só o 404, mensagem que sumiu, a atualiza). Devolve o problema, ou nulo.
       */
      reply: (
        message: ContactMessage,
        body: string,
        files: readonly File[],
      ): Promise<ApiProblem | null> => change(api.reply(message.id, body, files)),
    };
  }),
);
