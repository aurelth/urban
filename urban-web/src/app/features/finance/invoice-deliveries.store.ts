import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { InvoiceDeliveriesApi } from '../../core/finance/invoice-deliveries.api';
import {
  DEFAULT_INVOICE_DELIVERIES_QUERY,
  InvoiceDeliveriesQuery,
  MonthInvoiceDeliveries,
  SendMonthInvoicesResult,
} from '../../core/finance/invoice-deliveries.models';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { pendingCount } from './invoice-delivery-values';

const NOT_CLOSED_CODE = 'closing.not_closed';
const NOT_FOUND_STATUS = 404;
const CONFLICT_STATUS = 409;
const UNPROCESSABLE_STATUS = 422;

/**
 * - loading: primeira busca do mês (ou nova tentativa);
 * - ready: unidades na tela;
 * - not-closed: o mês ainda não foi fechado, então não há faturas para enviar;
 * - error: a busca falhou; a tela oferece tentar de novo.
 */
export type InvoiceDeliveriesStatus = 'loading' | 'ready' | 'not-closed' | 'error';

interface InvoiceDeliveriesState {
  status: InvoiceDeliveriesStatus;
  month: string;
  query: InvoiceDeliveriesQuery;
  data: MonthInvoiceDeliveries | null;
  /** Há uma chamada em andamento: a tela trava os controles e mantém a lista antiga. */
  busy: boolean;
}

/** O que o envio das faturas do mês respondeu: o resumo, ou o problema que a API explicou. */
export interface SendMonthOutcome {
  readonly result: SendMonthInvoicesResult | null;
  readonly problem: ApiProblem | null;
}

/** Estado da tela de envio de faturas. Fornecido no componente, então cada visita começa do zero. */
export const InvoiceDeliveriesStore = signalStore(
  withState<InvoiceDeliveriesState>({
    status: 'loading',
    month: '',
    query: DEFAULT_INVOICE_DELIVERIES_QUERY,
    data: null,
    busy: false,
  }),
  withComputed((store) => ({
    pageCount: computed(() =>
      Math.max(1, Math.ceil((store.data()?.total ?? 0) / store.query().pageSize)),
    ),
    /** Quantas faturas o envio do mês mandaria: as não enviadas e as que falharam. */
    pending: computed(() => {
      const data = store.data();
      return data === null ? 0 : pendingCount(data.counts);
    }),
    /** Há fatura na fila: a tela lê a lista de tempos em tempos até ela ser enviada. */
    waiting: computed(() => store.status() === 'ready' && (store.data()?.counts.queued ?? 0) > 0),
  })),
  withMethods((store) => {
    const api = inject(InvoiceDeliveriesApi);

    /** Lê o mês com a consulta dada; devolve falso quando a leitura falhou (o estado já mostra o motivo). */
    const read = async (month: string, query: InvoiceDeliveriesQuery): Promise<boolean> => {
      try {
        patchState(store, { data: await firstValueFrom(api.listMonth(month, query)), query });
        patchState(store, { status: 'ready' });
        return true;
      } catch (error) {
        patchState(store, {
          status: toApiProblem(error).code === NOT_CLOSED_CODE ? 'not-closed' : 'error',
        });
        return false;
      }
    };

    /** Lê de novo o mês e a página atuais; se a página ficou vazia, volta uma. */
    const refreshList = async (): Promise<void> => {
      const query = store.query();
      if (
        (await read(store.month(), query)) &&
        store.data()!.items.length === 0 &&
        query.page > 1
      ) {
        await read(store.month(), { ...query, page: query.page - 1 });
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
      /** Lê o mês na primeira página. Serve também para trocar de mês e tentar de novo. */
      show: async (month: string): Promise<void> => {
        patchState(store, { status: 'loading', month });
        await read(month, { ...store.query(), page: 1 });
      },
      goToPage: async (page: number): Promise<void> => {
        if (Number.isInteger(page) && page >= 1 && page <= store.pageCount()) {
          await whileBusy(() => read(store.month(), { ...store.query(), page }));
        }
      },
      refresh: (): Promise<void> => whileBusy(refreshList),
      /**
       * Lê a lista sem travar a tela, para acompanhar as faturas na fila. Não faz nada enquanto há outra chamada em andamento,
       * esquece uma leitura que falhou (a próxima tenta de novo) e descarta a que chega depois de a tela trocar de mês.
       */
      poll: async (): Promise<void> => {
        if (store.busy() || store.status() !== 'ready') {
          return;
        }
        const month = store.month();
        const page = store.query().page;
        try {
          const data = await firstValueFrom(api.listMonth(month, store.query()));
          if (
            store.status() === 'ready' &&
            store.month() === month &&
            store.query().page === page &&
            !store.busy()
          ) {
            patchState(store, { data });
          }
        } catch {
          // A próxima leitura tenta de novo; a lista que já está na tela continua valendo.
        }
      },
      /**
       * Põe na fila as faturas do mês e lê a lista de novo. Se o mês deixou de estar fechado, lê também, para a tela explicar.
       * Devolve o resumo, ou o problema da API.
       */
      sendMonth: (): Promise<SendMonthOutcome> =>
        whileBusy(async () => {
          let outcome: SendMonthOutcome;
          try {
            outcome = {
              result: await firstValueFrom(api.sendMonth(store.month())),
              problem: null,
            };
          } catch (error) {
            outcome = { result: null, problem: toApiProblem(error) };
          }
          if (outcome.problem === null || outcome.problem.status === CONFLICT_STATUS) {
            await refreshList();
          }
          return outcome;
        }),
      /**
       * Põe na fila a fatura de uma cobrança e lê a lista de novo. Se a tela estava desatualizada (a cobrança sumiu, já estava
       * na fila ou ficou sem e-mail), lê de novo também. Devolve o problema da API, ou nulo quando deu certo.
       */
      resend: (chargeId: string): Promise<ApiProblem | null> =>
        whileBusy(async () => {
          let problem: ApiProblem | null = null;
          try {
            await firstValueFrom(api.resend(chargeId));
          } catch (error) {
            problem = toApiProblem(error);
          }
          if (
            problem === null ||
            problem.status === NOT_FOUND_STATUS ||
            problem.status === CONFLICT_STATUS ||
            problem.status === UNPROCESSABLE_STATUS
          ) {
            await refreshList();
          }
          return problem;
        }),
    };
  }),
);
