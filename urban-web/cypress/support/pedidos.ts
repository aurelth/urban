import { MANAGER, StubUser } from './session';

export const REQUESTS_PATH = '/admin/pedidos-de-cadastro';
const REQUESTS_URL = '/api/admin/registration-requests';

export const ADMIN: StubUser = {
  ...MANAGER,
  email: 'dono@example.com',
  role: 'SuperAdmin',
  condominiumId: null,
  displayName: 'Aurel',
};

export interface StubRequest {
  id: string;
  email: string;
  name: string;
  condominiumName: string;
  requestedAt: string;
}

export const FIRST_REQUEST: StubRequest = {
  id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c4d01',
  email: 'marina@example.com',
  name: 'Marina',
  condominiumName: 'Residencial Aurora',
  requestedAt: '2026-11-13T04:23:00+00:00',
};

export const SECOND_REQUEST: StubRequest = {
  id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c4d02',
  email: 'carlos@example.com',
  name: 'Carlos',
  condominiumName: 'Edifício Bela Vista',
  requestedAt: '2026-11-13T15:05:00+00:00',
};

/** A API simulada guarda a fila: aprovar ou recusar tira o pedido, e a lista seguinte já vem sem ele. */
export function stubRequests(initial: StubRequest[] = [FIRST_REQUEST, SECOND_REQUEST]): void {
  let pending = [...initial];
  cy.intercept('GET', REQUESTS_URL, (request) =>
    request.reply({ statusCode: 200, body: pending }),
  ).as('requests');
  cy.intercept('POST', new RegExp(`${REQUESTS_URL}/[^/]+/(approve|reject)$`), (request) => {
    const id = request.url.split('/').at(-2);
    pending = pending.filter((item) => item.id !== id);
    request.reply({ statusCode: 204 });
  }).as('decision');
}

/** A decisão é recusada pela API (pedido já decidido, que sumiu ou sem permissão); a fila segue como está. */
export function stubDecisionRejected(
  statusCode: number,
  code: string,
  remaining: StubRequest[],
): void {
  cy.intercept('GET', REQUESTS_URL, { statusCode: 200, body: remaining }).as('requests');
  cy.intercept('POST', new RegExp(`${REQUESTS_URL}/[^/]+/(approve|reject)$`), {
    statusCode,
    body: { status: statusCode, code, detail: 'Detalhe da API' },
  }).as('decision');
}
