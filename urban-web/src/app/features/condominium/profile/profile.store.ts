import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { Observable, firstValueFrom } from 'rxjs';
import { CondominiumApi } from '../../../core/condominium/condominium.api';
import {
  MyCondominium,
  UpdateManagerIdentityPayload,
  UpdateMyCondominiumPayload,
} from '../../../core/condominium/condominium.models';
import { ApiProblem, toApiProblem } from '../../../core/http/api-problem';

/**
 * - loading: buscando os dados;
 * - ready: dados na tela, prontos para editar;
 * - saving: gravando (a tela trava o botão);
 * - conflict: outra pessoa mudou os dados depois da leitura (409 de versão); a tela pede para recarregar;
 * - not-found: o síndico não tem cadastro (404);
 * - error: a busca falhou por outro motivo.
 */
export type ProfileStatus = 'loading' | 'ready' | 'saving' | 'conflict' | 'not-found' | 'error';

export const CONCURRENCY_CONFLICT = 'concurrency.conflict';
const NOT_FOUND_STATUS = 404;
const CONFLICT_STATUS = 409;

interface ProfileState {
  status: ProfileStatus;
  data: MyCondominium | null;
}

/** Estado da tela "Meus dados". Fornecido no componente, então cada visita começa do zero. */
export const ProfileStore = signalStore(
  withState<ProfileState>({ status: 'loading', data: null }),
  withMethods((store) => {
    const api = inject(CondominiumApi);

    /** Grava e guarda a resposta; devolve o problema (ou nulo, se deu certo) para a tela explicar. */
    const save = async (request: Observable<MyCondominium>): Promise<ApiProblem | null> => {
      patchState(store, { status: 'saving' });
      try {
        const data = await firstValueFrom(request);
        patchState(store, { status: 'ready', data });
        return null;
      } catch (error) {
        const problem = toApiProblem(error);
        patchState(store, { status: statusAfterFailure(problem) });
        return problem;
      }
    };

    return {
      /** Busca os dados. Serve para a primeira carga, para tentar de novo e para recarregar depois de um conflito. */
      load: async (): Promise<void> => {
        patchState(store, { status: 'loading' });
        try {
          const data = await firstValueFrom(api.mine());
          patchState(store, { status: 'ready', data });
        } catch (error) {
          const notFound = toApiProblem(error).status === NOT_FOUND_STATUS;
          patchState(store, { status: notFound ? 'not-found' : 'error', data: null });
        }
      },
      saveFree: (payload: UpdateMyCondominiumPayload): Promise<ApiProblem | null> =>
        save(api.updateMine(payload)),
      saveIdentity: (payload: UpdateManagerIdentityPayload): Promise<ApiProblem | null> =>
        save(api.updateManagerIdentity(payload)),
    };
  }),
);

/** Só o 409 de versão é "conflito"; o 409 de CPF repetido é um erro de campo e a tela continua editável. */
function statusAfterFailure(problem: ApiProblem): ProfileStatus {
  if (problem.status === NOT_FOUND_STATUS) {
    return 'not-found';
  }
  if (problem.status === CONFLICT_STATUS && problem.code === CONCURRENCY_CONFLICT) {
    return 'conflict';
  }
  return 'ready';
}
