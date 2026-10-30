import { computed, inject } from '@angular/core';
import { patchState, signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { firstValueFrom } from 'rxjs';
import { RegistrationDraft } from '../drafts/registration-draft';
import { AuthApi } from './auth.api';
import { CurrentUser } from './auth.models';
import { withRefreshLock } from './refresh-lock';

export type SessionStatus = 'unknown' | 'anonymous' | 'authenticated';

interface SessionState {
  status: SessionStatus;
  user: CurrentUser | null;
  /** Só na memória: nunca vai para localStorage, sessionStorage nem cookie legível por script. */
  accessToken: string | null;
  /** Verdadeiro quando a sessão caiu sozinha (e não porque a pessoa saiu). */
  expired: boolean;
}

const ANONYMOUS = { status: 'anonymous', user: null, accessToken: null } as const;

export const SessionStore = signalStore(
  { providedIn: 'root' },
  withState<SessionState>({ status: 'unknown', user: null, accessToken: null, expired: false }),
  withComputed(({ user }) => ({
    needsCondominium: computed(() => {
      const current = user();
      return current?.role === 'Manager' && current.condominiumId === null;
    }),
  })),
  withMethods((store) => {
    const api = inject(AuthApi);
    const drafts = inject(RegistrationDraft);
    let restoring: Promise<void> | null = null;
    let renewing: Promise<boolean> | null = null;

    const requestRenewal = async (): Promise<boolean> => {
      try {
        const tokens = await withRefreshLock(() => firstValueFrom(api.refresh()));
        patchState(store, { accessToken: tokens.accessToken });
        return true;
      } catch {
        return false;
      }
    };

    // Uma renovação por vez: chamadas que chegam durante ela esperam o mesmo resultado.
    const renew = (): Promise<boolean> => {
      renewing ??= requestRenewal().finally(() => {
        renewing = null;
      });
      return renewing;
    };

    const loadUser = async (): Promise<void> => {
      try {
        const user = await firstValueFrom(api.me());
        patchState(store, { status: 'authenticated', user, expired: false });
      } catch (error) {
        patchState(store, ANONYMOUS);
        throw error;
      }
    };

    const restoreSession = async (): Promise<void> => {
      if (!(await renew())) {
        patchState(store, ANONYMOUS);
        return;
      }
      try {
        await loadUser();
      } catch {
        // loadUser já deixou a sessão anônima; quem restaura não precisa do motivo.
      }
    };

    return {
      renew,
      restore: (): Promise<void> => {
        restoring = restoreSession();
        return restoring;
      },
      /** Resolve quando a restauração do início do app termina (ou já, se nenhuma começou). */
      ready: (): Promise<void> => restoring ?? Promise.resolve(),
      signIn: async (email: string, password: string): Promise<void> => {
        const tokens = await firstValueFrom(api.login(email, password));
        patchState(store, { accessToken: tokens.accessToken });
        await loadUser();
      },
      /**
       * Pede um token novo e lê o usuário de novo, para a tela ver o que mudou na conta (o
       * condomínio e o nome do síndico, por exemplo). Se a sessão não se sustenta, ela cai como
       * expirada e quem está na área logada é levado à entrada.
       */
      reload: async (): Promise<boolean> => {
        try {
          if (!(await renew())) {
            throw new Error('refresh refused');
          }
          await loadUser();
          return true;
        } catch {
          patchState(store, { ...ANONYMOUS, expired: true });
          return false;
        }
      },
      signOut: async (): Promise<void> => {
        try {
          await firstValueFrom(api.logout());
        } catch {
          // Sai do lado do front mesmo que a API não responda: o token some da memória de qualquer jeito.
        }
        drafts.clearAll();
        patchState(store, { ...ANONYMOUS, expired: false });
      },
      /**
       * Encerra a sessão só deste navegador, sem chamar a API: serve para quando a própria API já revogou todas as
       * sessões da pessoa (troca de senha ou de e-mail). Apaga o rascunho como o sair, mas não marca a sessão como
       * expirada, para a tela não tratar como queda.
       */
      endSession: (): void => {
        drafts.clearAll();
        patchState(store, { ...ANONYMOUS, expired: false });
      },
      expire: (): void => {
        if (store.status() === 'authenticated') {
          patchState(store, { ...ANONYMOUS, expired: true });
        }
      },
    };
  }),
);
