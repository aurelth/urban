import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { Observable, firstValueFrom } from 'rxjs';

/**
 * - loading: busca do mês (a primeira, a de outro mês ou uma nova tentativa);
 * - ready: o mês na tela;
 * - error: a busca falhou; a tela oferece tentar de novo.
 */
export type MonthStatus = 'loading' | 'ready' | 'error';

interface MonthState<T> {
  status: MonthStatus;
  month: string;
  data: T | null;
}

/**
 * Estado de uma tela que mostra um mês lido de uma vez só (o painel financeiro e a fatura do morador). `source` roda no
 * contexto de injeção do store e devolve a leitura de um mês. O store é fornecido no componente, então cada visita começa
 * do zero.
 */
export function createMonthStore<T>(source: () => (month: string) => Observable<T>) {
  return signalStore(
    withState<MonthState<T>>({ status: 'loading', month: '', data: null }),
    withMethods((store) => {
      const read = source();

      return {
        /** Lê o mês. Resposta (ou falha) de um mês que a pessoa já deixou para trás é ignorada. */
        async show(month: string): Promise<void> {
          patchState(store, { status: 'loading', month });
          try {
            const data = await firstValueFrom(read(month));
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
}
