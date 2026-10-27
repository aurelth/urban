/** Respostas simuladas do cadastro: o E2E do CI sobe só o front, sem API. */
export const TERMS_VERSION = '2026-10-26';

export function stubTerms(version = TERMS_VERSION): void {
  cy.intercept('GET', '/api/terms/current', { statusCode: 200, body: { version } }).as('terms');
}

export function stubRegister(statusCode = 202, code?: string, retryAfter?: string): void {
  cy.intercept('POST', '/api/auth/register', {
    statusCode,
    headers: retryAfter ? { 'retry-after': retryAfter } : {},
    body: code ? { status: statusCode, code, detail: 'Detalhe da API' } : undefined,
  }).as('register');
}

export function stubConfirmEmail(statusCode = 204, code?: string): void {
  cy.intercept('POST', '/api/auth/confirm-email', {
    statusCode,
    body: code ? { status: statusCode, code, detail: 'Detalhe da API' } : undefined,
  }).as('confirm');
}
