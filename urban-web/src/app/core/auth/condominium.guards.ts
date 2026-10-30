import { inject } from '@angular/core';
import { CanActivateFn, Router } from '@angular/router';
import { SessionStore } from './session.store';

export const CONDOMINIUM_SETUP_PATH = '/condominio/cadastrar';

/**
 * Área logada: o síndico que ainda não cadastrou o condomínio só enxerga o formulário de cadastro,
 * o resto do app depende do condomínio. Quem não está logado fica para a guarda de login.
 */
export const condominiumGuard: CanActivateFn = async () => {
  const session = inject(SessionStore);
  const router = inject(Router);

  await session.ready();

  return session.needsCondominium() ? router.createUrlTree([CONDOMINIUM_SETUP_PATH]) : true;
};

/** O formulário de cadastro do condomínio é só para o síndico que ainda não o fez. */
export const condominiumSetupGuard: CanActivateFn = async () => {
  const session = inject(SessionStore);
  const router = inject(Router);

  await session.ready();

  return session.needsCondominium() ? true : router.createUrlTree(['/dashboard']);
};

/**
 * As telas de dados do condomínio são do síndico. Morador e administrador vão para o painel. Quem não está logado passa
 * por aqui e cai na guarda de login, que é quem decide para onde levar.
 */
export const managerGuard: CanActivateFn = async () => {
  const session = inject(SessionStore);
  const router = inject(Router);

  await session.ready();

  const user = session.user();

  return user === null || user.role === 'Manager' ? true : router.createUrlTree(['/dashboard']);
};
