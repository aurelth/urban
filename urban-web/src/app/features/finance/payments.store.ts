import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { PaymentsApi } from '../../core/finance/payments.api';
import {
  DEFAULT_PAYMENTS_QUERY,
  MonthPayments,
  PaymentsQuery,
  PaymentsStatusFilter,
  RegisterPaymentPayload,
} from '../../core/finance/payments.models';
import { toReceive } from './payments-values';

const NOT_CLOSED_CODE = 'closing.not_closed';
const NOT_FOUND_STATUS = 404;
const UNPROCESSABLE_STATUS = 422;

/**
 * - loading: primeira busca do mês (ou nova tentativa);
 * - ready: cobranças na tela;
 * - not-closed: o mês ainda não foi fechado, então não há cobranças;
 * - error: a busca falhou; a tela oferece tentar de novo.
 */
export type PaymentsStatus = 'loading' | 'ready' | 'not-closed' | 'error';

interface PaymentsState {
  status: PaymentsStatus;
  month: string;
  query: PaymentsQuery;
  data: MonthPayments | null;
  /** Há uma busca em andamento: a tela trava os controles e mantém a lista antiga. */
  busy: boolean;
}

/** Estado da tela de pagamentos do mês. Fornecido no componente, então cada visita começa do zero. */
export const PaymentsStore = signalStore(
  withState<PaymentsState>({
    status: 'loading',
    month: '',
    query: DEFAULT_PAYMENTS_QUERY,
    data: null,
    busy: false,
  }),
  withComputed((store) => ({
    pageCount: computed(() =>
      Math.max(1, Math.ceil((store.data()?.total ?? 0) / store.query().pageSize)),
    ),
    hasFilter: computed(() => store.query().status !== 'all'),
    toReceive: computed(() => {
      const data = store.data();
      return data === null ? 0 : toReceive(data);
    }),
  })),
  withMethods((store) => {
    const api = inject(PaymentsApi);

    /** Lê o mês com a consulta dada; devolve falso quando a leitura falhou (o estado já mostra o motivo). */
    const read = async (month: string, query: PaymentsQuery): Promise<boolean> => {
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

    const load = async (month: string, query: PaymentsQuery): Promise<void> => {
      patchState(store, { busy: true });
      await read(month, query);
      patchState(store, { busy: false });
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

    const refresh = async (): Promise<void> => {
      patchState(store, { busy: true });
      await refreshList();
      patchState(store, { busy: false });
    };

    return {
      /** Lê o mês, mantendo a situação escolhida na primeira página. Serve também para trocar de mês e tentar de novo. */
      show: async (month: string): Promise<void> => {
        patchState(store, { status: 'loading', month });
        await read(month, { ...store.query(), page: 1 });
      },
      filter: (status: PaymentsStatusFilter): Promise<void> =>
        load(store.month(), { ...store.query(), status, page: 1 }),
      goToPage: async (page: number): Promise<void> => {
        if (Number.isInteger(page) && page >= 1 && page <= store.pageCount()) {
          await load(store.month(), { ...store.query(), page });
        }
      },
      refresh,
      /**
       * Registra um pagamento e lê o mês de novo. Se a tela estava desatualizada (a cobrança sumiu ou o saldo mudou), lê o
       * mês de novo também. Devolve o problema da API, ou nulo quando deu certo.
       */
      register: async (
        chargeId: string,
        payload: RegisterPaymentPayload,
      ): Promise<ApiProblem | null> => {
        let problem: ApiProblem | null = null;
        patchState(store, { busy: true });
        try {
          await firstValueFrom(api.register(chargeId, payload));
        } catch (error) {
          problem = toApiProblem(error);
        }
        if (
          problem === null ||
          problem.status === NOT_FOUND_STATUS ||
          problem.status === UNPROCESSABLE_STATUS
        ) {
          await refreshList();
        }
        patchState(store, { busy: false });
        return problem;
      },
    };
  }),
);
