import { MANAGER, StubUser } from './session';

/** Síndica que acabou de confirmar o e-mail: ainda não tem condomínio nem nome de exibição. */
export const NEW_MANAGER: StubUser = { ...MANAGER, condominiumId: null, displayName: null };

export const REGISTERED_SUMMARY = {
  condominiumId: MANAGER.condominiumId,
  name: 'Residencial das Flores',
  cnpj: '11.222.333/0001-81',
  managerCpf: '***.982.247-**',
};

/** Resposta do cadastro do condomínio. O E2E do CI sobe só o front, sem API. */
export function stubRegisterCondominium(statusCode = 201, code?: string): void {
  cy.intercept('POST', '/api/condominiums', {
    statusCode,
    body: code ? { status: statusCode, code, detail: 'Detalhe da API' } : REGISTERED_SUMMARY,
  }).as('registerCondominium');
}

/**
 * Cadastro que dá certo e muda o que a API responde depois dele: antes, o usuário não tem
 * condomínio; depois, tem condomínio e nome. A renovação da sessão e o `me` seguem a mesma chave.
 */
export function stubRegistrationThatWorks(): void {
  let registered = false;
  cy.intercept('GET', '/api/auth/me', (request) => {
    request.reply({ statusCode: 200, body: registered ? MANAGER : NEW_MANAGER });
  });
  cy.intercept('POST', '/api/condominiums', (request) => {
    registered = true;
    request.reply({ statusCode: 201, body: REGISTERED_SUMMARY });
  }).as('registerCondominium');
}
