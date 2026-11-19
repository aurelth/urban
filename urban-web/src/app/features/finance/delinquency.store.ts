import { computed } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import {
  DEFAULT_DELINQUENCY_QUERY,
  Delinquency,
  DelinquencyQuery,
} from '../../core/finance/delinquency.models';
import { ApiProblem } from '../../core/http/api-problem';

/**
 * - loading: primeira busca (ou nova tentativa);
 * - ready: unidades devedoras na tela;
 * - error: a busca falhou; a tela oferece tentar de novo.
 */
export type DelinquencyStatus = 'loading' | 'ready' | 'error';

interface DelinquencyState {
  status: DelinquencyStatus;
  query: DelinquencyQuery;
  data: Delinquency | null;
  /** Há uma chamada em andamento: a tela trava os controles e mantém a lista antiga. */
  busy: boolean;
}

/** Estado da tela de inadimplência. Fornecido no componente, então cada visita começa do zero. */
export const DelinquencyStore = signalStore(
  withState<DelinquencyState>({
    status: 'loading',
    query: DEFAULT_DELINQUENCY_QUERY,
    data: null,
    busy: false,
  }),
  withComputed((store) => ({
    pageCount: computed(() =>
      Math.max(1, Math.ceil((store.data()?.units ?? 0) / store.query().pageSize)),
    ),
  })),
  withMethods((store) => {
    const notImplemented = (): never => {
      patchState(store, { busy: false });
      throw new Error('Não implementado');
    };
    return {
      load: (): Promise<void> => Promise.resolve(notImplemented()),
      goToPage: (page: number): Promise<void> => Promise.resolve(notImplemented()),
      refresh: (): Promise<void> => Promise.resolve(notImplemented()),
      resend: (chargeId: string): Promise<ApiProblem | null> => Promise.resolve(notImplemented()),
    };
  }),
);
