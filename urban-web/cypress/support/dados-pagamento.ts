import { stubFinanceRejected } from './financeiro';

export const INSTRUCTIONS_PATH = '/financeiro/dados-de-pagamento';
export const INSTRUCTIONS_API = '/api/finance/payment-instructions';

export interface StubInstructions {
  pix: { kind: string; key: string } | null;
  account: { bank: string; agency: string; number: string; kind: string } | null;
  updatedAt: string | null;
}

/** Chave Pix de CNPJ e conta corrente, atualizados às 10:30 de Brasília de 16/11/2026. */
export const SAVED_INSTRUCTIONS: StubInstructions = {
  pix: { kind: 'cnpj', key: '11222333000181' },
  account: { bank: 'Banco do Brasil', agency: '1234-5', number: '98765-4', kind: 'checking' },
  updatedAt: '2026-11-16T13:30:00+00:00',
};

export const NO_INSTRUCTIONS: StubInstructions = { pix: null, account: null, updatedAt: null };

/** O E2E do CI sobe só o front, sem API: cada tela simula a sua. */
export function stubInstructions(body: StubInstructions = SAVED_INSTRUCTIONS): void {
  cy.intercept('GET', INSTRUCTIONS_API, { statusCode: 200, body }).as('instructions');
}

/** Gravação que dá certo: a API devolve o que recebeu, com a hora da gravação (11:00 de Brasília). */
export function stubInstructionsSaves(): void {
  cy.intercept('PUT', INSTRUCTIONS_API, (request) => {
    request.reply({
      statusCode: 200,
      body: { ...request.body, updatedAt: '2026-11-16T14:00:00+00:00' },
    });
  }).as('saveInstructions');
}

export function stubInstructionsRejected(code: string): void {
  stubFinanceRejected('PUT', INSTRUCTIONS_API, 400, code, 'saveInstructions');
}

/** O rótulo do Material cobre o meio do campo vazio, então o clique vai para a direita (como em "Meus dados"). */
export function enter(name: string, value: string): void {
  cy.get(`input[formControlName="${name}"]`).click('right').type(`{selectall}${value}`);
}
