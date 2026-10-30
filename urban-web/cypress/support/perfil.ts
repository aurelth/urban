import { MANAGER } from './session';

const ADDRESS = {
  postalCode: '89010000',
  street: 'Rua das Flores',
  number: '120',
  complement: 'Apto 42',
  neighborhood: 'Centro',
  city: 'Blumenau',
  state: 'SC',
};

/** Resposta de GET /api/condominiums/mine. O E2E do CI sobe só o front, sem API. */
export const PROFILE = {
  condominiumId: MANAGER.condominiumId,
  version: '7.9',
  displayName: 'Marina',
  manager: {
    fullName: 'Marina Souza',
    maskedCpf: '***.982.247-**',
    birthDate: '1980-06-15',
    phone: '47991234567',
    address: ADDRESS,
  },
  condominium: {
    name: 'Residencial das Flores',
    cnpj: '11.222.333/0001-81',
    address: {
      postalCode: '89012100',
      street: 'Rua XV de Novembro',
      number: '500',
      complement: null,
      neighborhood: 'Centro',
      city: 'Blumenau',
      state: 'SC',
    },
  },
};

export const PROFILE_PATH = '/condominio/meus-dados';

export function stubProfile(): void {
  cy.intercept('GET', '/api/condominiums/mine', { statusCode: 200, body: PROFILE }).as('profile');
}

/**
 * Salvamento que dá certo: a API devolve os dados novos e, depois dele, o `me` passa a trazer o nome novo.
 * Sem este stub, o `me` continuaria devolvendo o nome antigo.
 */
export function stubProfileSaves(displayName: string, cpfMasked = PROFILE.manager.maskedCpf): void {
  let saved = false;
  const updated = {
    ...PROFILE,
    version: '8.9',
    displayName,
    manager: { ...PROFILE.manager, maskedCpf: cpfMasked },
  };
  cy.intercept('GET', '/api/auth/me', (request) => {
    request.reply({ statusCode: 200, body: saved ? { ...MANAGER, displayName } : MANAGER });
  });
  cy.intercept('PUT', '/api/condominiums/mine', (request) => {
    saved = true;
    request.reply({ statusCode: 200, body: updated });
  }).as('saveProfile');
  cy.intercept('PUT', '/api/condominiums/mine/manager-identity', (request) => {
    saved = true;
    request.reply({ statusCode: 200, body: updated });
  }).as('saveIdentity');
}

export function stubProfileRejected(path: string, statusCode: number, code: string): void {
  cy.intercept('PUT', path, {
    statusCode,
    body: { status: statusCode, code, detail: 'Detalhe da API' },
  }).as(path.endsWith('identity') ? 'saveIdentity' : 'saveProfile');
}

export function stubChangePassword(statusCode = 204, code?: string): void {
  cy.intercept('POST', '/api/auth/change-password', {
    statusCode,
    body: code ? { status: statusCode, code, detail: 'Detalhe da API' } : undefined,
  }).as('changePassword');
}

export function stubChangeEmail(statusCode = 202, code?: string): void {
  cy.intercept('POST', '/api/auth/change-email', {
    statusCode,
    body: code ? { status: statusCode, code, detail: 'Detalhe da API' } : undefined,
  }).as('changeEmail');
}

export function stubConfirmEmailChange(statusCode = 204, code?: string): void {
  cy.intercept('POST', '/api/auth/confirm-email-change', {
    statusCode,
    body: code ? { status: statusCode, code, detail: 'Detalhe da API' } : undefined,
  }).as('confirmEmailChange');
}
