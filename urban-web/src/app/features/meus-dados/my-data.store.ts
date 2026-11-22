import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { toApiProblem } from '../../core/http/api-problem';
import { ResidentsApi } from '../../core/residents/residents.api';
import { Resident } from '../../core/residents/residents.models';

const NOT_FOUND_STATUS = 404;

/** loading: buscando; ready: cadastro na tela; not-found: a conta não tem cadastro de morador (404); error: outra falha. */
export type MyDataStatus = 'loading' | 'ready' | 'not-found' | 'error';

interface MyDataState {
  status: MyDataStatus;
  resident: Resident | null;
}

/** Estado da tela "Meus dados" do morador. Fornecido no componente, então cada visita começa do zero. */
export const MyDataStore = signalStore(
  withState<MyDataState>({ status: 'loading', resident: null }),
  withMethods((store) => {
    const residents = inject(ResidentsApi);

    return {
      /** Busca o próprio cadastro. Serve à primeira carga e a tentar de novo. */
      load: async (): Promise<void> => {
        patchState(store, { status: 'loading' });
        try {
          patchState(store, {
            status: 'ready',
            resident: await firstValueFrom(residents.getMine()),
          });
        } catch (error) {
          const notFound = toApiProblem(error).status === NOT_FOUND_STATUS;
          patchState(store, { status: notFound ? 'not-found' : 'error', resident: null });
        }
      },
    };
  }),
);
