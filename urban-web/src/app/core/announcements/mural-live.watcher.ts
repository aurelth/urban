import {
  EnvironmentProviders,
  Injectable,
  computed,
  effect,
  inject,
  provideAppInitializer,
  untracked,
} from '@angular/core';
import { SessionStore } from '../auth/session.store';
import { MuralLive } from './mural-live';

/**
 * Liga e desliga o mural ao vivo conforme a sessão: só quem pode morar no condomínio (morador, ou síndico com condomínio
 * cadastrado) o recebe, e sair da conta o fecha.
 */
@Injectable({ providedIn: 'root' })
export class MuralLiveWatcher {
  private readonly session = inject(SessionStore);
  private readonly live = inject(MuralLive);

  /** O papel e o condomínio vêm do `me`; trocar de nome ou de e-mail não muda o resultado e não reabre a conexão. */
  private readonly wanted = computed(() => {
    const user = this.session.user();
    if (this.session.status() !== 'authenticated' || user === null) {
      return false;
    }
    return user.role === 'Resident' || (user.role === 'Manager' && user.condominiumId !== null);
  });

  constructor() {
    effect(() => {
      const wanted = this.wanted();
      untracked(() => (wanted ? this.live.start() : this.live.stop()));
    });
  }
}

export function provideMuralLive(): EnvironmentProviders {
  return provideAppInitializer(() => {
    inject(MuralLiveWatcher);
  });
}
