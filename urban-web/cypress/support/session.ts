/**
 * Respostas simuladas da API de autenticação. O E2E do CI sobe só o front (sem API),
 * então cada teste declara o que a API responderia.
 */
export interface StubUser {
  id: string;
  email: string;
  role: 'SuperAdmin' | 'Manager' | 'Resident';
  condominiumId: string | null;
}

export const MANAGER: StubUser = {
  id: '01a176be-52fe-7784-a4d6-ed99baca79c9',
  email: 'sindica@example.com',
  role: 'Manager',
  condominiumId: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c4d5e',
};

const TOKENS = { accessToken: 'token-de-teste', accessTokenExpiresAt: '2099-01-01T00:00:00+00:00' };

/** Quem abre o app sem sessão: a renovação pelo cookie é recusada. */
export function stubAnonymous(): void {
  cy.intercept('POST', '/api/auth/refresh', {
    statusCode: 401,
    body: { status: 401, code: 'identity.session.unauthenticated', detail: 'Sessão inválida' },
  });
}

/** Quem abre o app com o cookie de renovação ainda válido (F5 ou volta ao site). */
export function stubSignedIn(user: StubUser = MANAGER): void {
  cy.intercept('POST', '/api/auth/refresh', { statusCode: 200, body: TOKENS });
  cy.intercept('GET', '/api/auth/me', { statusCode: 200, body: user });
  cy.intercept('POST', '/api/auth/logout', { statusCode: 204 });
}

/** Login que dá certo: depois dele, a API passa a devolver o usuário. */
export function stubLogin(user: StubUser = MANAGER): void {
  cy.intercept('POST', '/api/auth/login', { statusCode: 200, body: TOKENS }).as('login');
  cy.intercept('GET', '/api/auth/me', { statusCode: 200, body: user });
  cy.intercept('POST', '/api/auth/logout', { statusCode: 204 }).as('logout');
}

export function stubLoginRejected(status: number, code: string): void {
  cy.intercept('POST', '/api/auth/login', {
    statusCode: status,
    body: { status, code, detail: 'Detalhe da API' },
  }).as('login');
}
