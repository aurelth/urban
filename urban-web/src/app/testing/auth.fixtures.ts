import { HttpTestingController } from '@angular/common/http/testing';
import { CurrentUser } from '../core/auth/auth.models';
import { SessionStore } from '../core/auth/session.store';
import { flushPromises } from './async';

export const MANAGER: CurrentUser = {
  id: '01a176be-52fe-7784-a4d6-ed99baca79c9',
  email: 'sindica@example.com',
  role: 'Manager',
  condominiumId: null,
  displayName: null,
};

/** Síndica que já cadastrou o condomínio: tem condomínio e o nome pelo qual prefere ser chamada. */
export const REGISTERED_MANAGER: CurrentUser = {
  ...MANAGER,
  condominiumId: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c4d5e',
  displayName: 'Marina',
};

export function tokenResponse(accessToken = 'access-1'): {
  accessToken: string;
  accessTokenExpiresAt: string;
} {
  return { accessToken, accessTokenExpiresAt: '2026-10-26T12:15:00+00:00' };
}

export function problem(
  status: number,
  code: string,
  detail = 'detalhe da API',
): { status: number; code: string; detail: string } {
  return { status, code, detail };
}

/** Entra pela API simulada, como a tela de login faz, e deixa o store autenticado. */
export async function signInAs(
  store: InstanceType<typeof SessionStore>,
  http: HttpTestingController,
  user: CurrentUser = MANAGER,
): Promise<void> {
  const signingIn = store.signIn(user.email, 'Senha-segura-123');
  http.expectOne('/api/auth/login').flush(tokenResponse());
  await flushPromises();
  http.expectOne('/api/auth/me').flush(user);
  await signingIn;
}
