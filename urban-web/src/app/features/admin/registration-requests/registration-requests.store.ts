import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { Observable, firstValueFrom } from 'rxjs';
import { RegistrationRequest } from '../../../core/admin/registration-requests.models';
import { RegistrationRequestsApi } from '../../../core/admin/registration-requests.api';
import { ApiProblem, toApiProblem } from '../../../core/http/api-problem';

/**
 * - loading: primeira busca (ou nova tentativa depois de um erro);
 * - ready: a lista está na tela, vazia ou não;
 * - error: a busca falhou; a tela oferece tentar de novo.
 */
export type RegistrationRequestsStatus = 'loading' | 'ready' | 'error';

/** Recusas que significam "a tela está desatualizada" (o pedido sumiu ou outro já o decidiu): a lista é buscada de novo. */
const STALE_STATUSES: ReadonlySet<number> = new Set([404, 409]);

interface RegistrationRequestsState {
  status: RegistrationRequestsStatus;
  items: readonly RegistrationRequest[];
  /** Há uma decisão em andamento: a tela trava os botões de todos os pedidos. */
  busy: boolean;
}

/** Estado da lista de pedidos de cadastro. Fornecido no componente, então cada visita começa do zero. */
export const RegistrationRequestsStore = signalStore(
  withState<RegistrationRequestsState>({ status: 'loading', items: [], busy: false }),
  withMethods((store) => {
    const api = inject(RegistrationRequestsApi);

    /** Faz uma decisão; se deu certo (ou a tela estava desatualizada), busca a lista de novo. Devolve o problema, ou nulo. */
    const decide = async (request: Observable<void>): Promise<ApiProblem | null> => {
      patchState(store, { busy: true });
      let problem: ApiProblem | null = null;
      try {
        await firstValueFrom(request);
      } catch (error) {
        problem = toApiProblem(error);
      }
      if (problem === null || STALE_STATUSES.has(problem.status)) {
        try {
          patchState(store, { items: await firstValueFrom(api.list()) });
        } catch {
          patchState(store, { status: 'error' });
        }
      }
      patchState(store, { busy: false });
      return problem;
    };

    return {
      /** Primeira busca. Serve também para tentar de novo depois de um erro. */
      load: async (): Promise<void> => {
        patchState(store, { status: 'loading' });
        try {
          patchState(store, { status: 'ready', items: await firstValueFrom(api.list()) });
        } catch {
          patchState(store, { status: 'error' });
        }
      },
      approve: (target: RegistrationRequest): Promise<ApiProblem | null> =>
        decide(api.approve(target.id)),
      reject: (target: RegistrationRequest): Promise<ApiProblem | null> =>
        decide(api.reject(target.id)),
    };
  }),
);
