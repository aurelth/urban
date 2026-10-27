import { HttpTestingController } from '@angular/common/http/testing';
import { CurrentUser } from '../core/auth/auth.models';
import { SessionStore } from '../core/auth/session.store';
import { flushPromises } from './async';

export const MANAGER: CurrentUser = {
  id: '01a176be-52fe-7784-a4d6-ed99baca79c9',
  email: 'sindica@example.com',
  role: 'Manager',
  condominiumId: null,
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
