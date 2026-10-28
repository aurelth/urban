/** Respostas simuladas da recuperação de senha: o E2E do CI sobe só o front, sem API. */
export function stubForgotPassword(statusCode = 202, code?: string, retryAfter?: string): void {
  cy.intercept('POST', '/api/auth/forgot-password', {
    statusCode,
    headers: retryAfter ? { 'retry-after': retryAfter } : {},
    body: code ? { status: statusCode, code, detail: 'Detalhe da API' } : undefined,
  }).as('forgot');
}
