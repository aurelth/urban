import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { PaymentInstructionsApi } from '../../core/finance/payment-instructions.api';
import {
  PaymentInstructions,
  PaymentInstructionsPayload,
} from '../../core/finance/payment-instructions.models';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';

/**
 * - loading: buscando os dados;
 * - ready: dados na tela, prontos para editar;
 * - saving: gravando (a tela trava o botão);
 * - error: a busca falhou.
 */
export type PaymentInstructionsStatus = 'loading' | 'ready' | 'saving' | 'error';

interface PaymentInstructionsState {
  status: PaymentInstructionsStatus;
  instructions: PaymentInstructions | null;
}

/** Estado da tela "Dados para pagamento". Fornecido no componente, então cada visita começa do zero. */
export const PaymentInstructionsStore = signalStore(
  withState<PaymentInstructionsState>({ status: 'loading', instructions: null }),
  withMethods((store) => {
    const api = inject(PaymentInstructionsApi);

    return {
      /** Busca os dados. Serve para a primeira carga e para tentar de novo depois de um erro. */
      load: async (): Promise<void> => {
        patchState(store, { status: 'loading' });
        try {
          const instructions = await firstValueFrom(api.get());
          patchState(store, { status: 'ready', instructions });
        } catch {
          patchState(store, { status: 'error', instructions: null });
        }
      },
      /** Grava e guarda a resposta; devolve o problema (ou nulo, se deu certo) para a tela explicar. */
      save: async (payload: PaymentInstructionsPayload): Promise<ApiProblem | null> => {
        patchState(store, { status: 'saving' });
        try {
          const instructions = await firstValueFrom(api.save(payload));
          patchState(store, { status: 'ready', instructions });
          return null;
        } catch (error) {
          patchState(store, { status: 'ready' });
          return toApiProblem(error);
        }
      },
    };
  }),
);
