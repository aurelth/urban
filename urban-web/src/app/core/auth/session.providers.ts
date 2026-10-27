import { EnvironmentProviders, inject, provideAppInitializer } from '@angular/core';
import { SessionStore } from './session.store';

/**
 * Ao abrir o app (ou dar F5) a memória está vazia, mas o cookie de renovação continua no navegador.
 * A restauração começa na hora e as rotas protegidas esperam por ela (SessionStore.ready),
 * sem travar a abertura das páginas públicas.
 */
export function provideSessionRestore(): EnvironmentProviders {
  return provideAppInitializer(() => {
    void inject(SessionStore).restore();
  });
}
