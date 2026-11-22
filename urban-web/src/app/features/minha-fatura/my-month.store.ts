import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { MyMonthApi } from '../../core/finance/my-month.api';
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
  withMethods((store) => {
    const api = inject(MyMonthApi);

    return {
      /** Lê o mês. Resposta (ou falha) de um mês que a pessoa já deixou para trás é ignorada. */
      async show(month: string): Promise<void> {
        patchState(store, { status: 'loading', month });
        try {
          const data = await firstValueFrom(api.get(month));
          if (store.month() === month) {
            patchState(store, { status: 'ready', data });
          }
        } catch {
          if (store.month() === month) {
            patchState(store, { status: 'error' });
          }
        }
      },
    };
  }),
);
