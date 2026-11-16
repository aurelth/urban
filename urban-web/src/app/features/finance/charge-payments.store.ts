import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { ApiProblem, toApiProblem } from '../../core/http/api-problem';
import { PaymentsApi } from '../../core/finance/payments.api';
import { ChargePayments } from '../../core/finance/payments.models';

const NOT_FOUND_STATUS = 404;
const CONFLICT_STATUS = 409;

/** loading, ready, not-found (a cobrança não existe mais) ou error (a leitura falhou; dá para tentar de novo). */
export type ChargePaymentsStatus = 'loading' | 'ready' | 'not-found' | 'error';

interface ChargePaymentsState {
  status: ChargePaymentsStatus;
  chargeId: string;
  data: ChargePayments | null;
  busy: boolean;
}

/** Estado do diálogo com os pagamentos de uma cobrança. Fornecido no componente do diálogo. */
export const ChargePaymentsStore = signalStore(
  withState<ChargePaymentsState>({ status: 'loading', chargeId: '', data: null, busy: false }),
  withMethods((store) => {
    const api = inject(PaymentsApi);

    const read = async (): Promise<void> => {
      try {
        patchState(store, {
          data: await firstValueFrom(api.listCharge(store.chargeId())),
          status: 'ready',
        });
      } catch (error) {
        patchState(store, {
          status: toApiProblem(error).status === NOT_FOUND_STATUS ? 'not-found' : 'error',
        });
      }
    };

    return {
      /** Lê os pagamentos da cobrança. Serve também para tentar de novo depois de um erro. */
      load: async (chargeId: string): Promise<void> => {
        patchState(store, { status: 'loading', chargeId });
        await read();
      },
      /**
       * Estorna um pagamento. Depois do estorno, ou se a tela estava desatualizada (o pagamento já foi estornado ou sumiu),
       * lê a cobrança de novo. Devolve o problema da API, ou nulo quando deu certo.
       */
      reverse: async (paymentId: string, reason: string): Promise<ApiProblem | null> => {
        patchState(store, { busy: true });
        let problem: ApiProblem | null = null;
        try {
          await firstValueFrom(api.reverse(paymentId, reason));
        } catch (error) {
          problem = toApiProblem(error);
        }
        if (
          problem === null ||
          problem.status === NOT_FOUND_STATUS ||
          problem.status === CONFLICT_STATUS
        ) {
          await read();
        }
        patchState(store, { busy: false });
        return problem;
      },
    };
  }),
);
