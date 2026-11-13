import { inject } from '@angular/core';
import { patchState, signalStore, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { AdminApi } from '../../../core/admin/admin.api';
import { PlatformSummary } from '../../../core/admin/admin.models';

export type OverviewStatus = 'loading' | 'ready' | 'error';

interface OverviewState {
  status: OverviewStatus;
  summary: PlatformSummary | null;
}

/** Estado da visão geral da plataforma. Fornecido no componente, então cada visita busca números novos. */
export const OverviewStore = signalStore(
  withState<OverviewState>({ status: 'loading', summary: null }),
  withMethods((store) => {
    const api = inject(AdminApi);
    return {
      /** Busca os números. Serve também para tentar de novo depois de um erro. */
      load: async (): Promise<void> => {
        patchState(store, { status: 'loading' });
        try {
          patchState(store, { status: 'ready', summary: await firstValueFrom(api.summary()) });
        } catch {
          patchState(store, { status: 'error' });
        }
      },
    };
  }),
);
