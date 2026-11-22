import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { MyMonth } from '../../core/finance/my-month.models';

/**
 * - loading: busca do mês (a primeira, a de outro mês ou uma nova tentativa);
 * - ready: o mês na tela;
 * - error: a busca falhou; a tela oferece tentar de novo.
 */
export type MyMonthStatus = 'loading' | 'ready' | 'error';

interface MyMonthState {
  status: MyMonthStatus;
  month: string;
  data: MyMonth | null;
}

/** Estado da tela "Minha fatura". Fornecido no componente, então cada visita começa do zero. */
export const MyMonthStore = signalStore(
  withState<MyMonthState>({ status: 'loading', month: '', data: null }),
  withMethods((store) => ({
    // Esqueleto da fatia de testes: a leitura da API vem com a implementação.
    show(month: string): Promise<void> {
      patchState(store, { status: 'loading', month });
      return Promise.resolve();
    },
  })),
);
