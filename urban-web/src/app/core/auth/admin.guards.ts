import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from './session.store';

/**
 * As telas de administração são do administrador da plataforma. Síndico e morador vão para o painel. Quem não está
 * logado passa por aqui e cai na guarda de login, que é quem decide para onde levar.
 */
export const adminGuard: CanActivateFn = async () => {
  const session = inject(SessionStore);
  const router = inject(Router);

  await session.ready();

  const user = session.user();

  return user === null || user.role === 'SuperAdmin' ? true : router.createUrlTree(['/dashboard']);
};

/**
 * O painel de condomínio não serve ao administrador: a tela inicial dele é a visão geral da plataforma. Os demais (síndico,
 * morador e quem não está logado) seguem para o painel, onde as guardas de login e de condomínio decidem o resto.
 */
export const adminHomeGuard: CanActivateFn = async () => {
  const session = inject(SessionStore);
  const router = inject(Router);

  await session.ready();

  return session.user()?.role === 'SuperAdmin'
    ? router.createUrlTree(['/admin/visao-geral'])
    : true;
};
