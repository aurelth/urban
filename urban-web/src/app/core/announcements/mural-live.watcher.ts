import { EnvironmentProviders, Injectable, provideAppInitializer, inject } from '@angular/core';

/**
 * Liga e desliga o mural ao vivo conforme a sessão: só quem pode morar no condomínio (morador, ou síndico com condomínio
 * cadastrado) o recebe, e sair da conta o fecha.
 */
@Injectable({ providedIn: 'root' })
export class MuralLiveWatcher {}

export function provideMuralLive(): EnvironmentProviders {
  return provideAppInitializer(() => {
    inject(MuralLiveWatcher);
  });
}
