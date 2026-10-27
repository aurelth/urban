import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from './session.store';

/** Rotas da área logada: quem não está logado vai para a entrada e volta ao destino depois. */
export const authGuard: CanActivateFn = async (_route, state) => {
  const session = inject(SessionStore);
  const router = inject(Router);

  await session.ready();

  return session.status() === 'authenticated'
    ? true
    : router.createUrlTree(['/entrar'], { queryParams: { returnUrl: state.url } });
};

/** Telas só para quem não está logado (entrada, cadastro): quem já entrou vai para o painel. */
export const guestGuard: CanActivateFn = async () => {
  const session = inject(SessionStore);
  const router = inject(Router);

  await session.ready();

  return session.status() === 'authenticated' ? router.createUrlTree(['/dashboard']) : true;
};
