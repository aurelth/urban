import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { Resident } from '../../core/residents/residents.models';

/** loading: buscando; ready: cadastro na tela; not-found: a conta não tem cadastro de morador (404); error: outra falha. */
export type MyDataStatus = 'loading' | 'ready' | 'not-found' | 'error';

interface MyDataState {
  status: MyDataStatus;
  resident: Resident | null;
}

/** Estado da tela "Meus dados" do morador. Fornecido no componente, então cada visita começa do zero. */
export const MyDataStore = signalStore(
  withState<MyDataState>({ status: 'loading', resident: null }),
  withMethods((store) => ({
    /** Busca o próprio cadastro. Serve à primeira carga e a tentar de novo. */
    load: async (): Promise<void> => {
      patchState(store, { status: 'loading' });
    },
  })),
);
