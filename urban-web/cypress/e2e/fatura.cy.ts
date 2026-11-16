import { stubInvoice, stubInvoiceRejected } from '../support/fatura';
import { TODAY } from '../support/financeiro';
import { CHARGE_101, CHARGE_102, PAYMENTS_PATH, stubPayments } from '../support/pagamentos';
import { stubSignedIn } from '../support/session';

describe('Fatura em PDF', () => {
  beforeEach(() => {
    cy.clock(TODAY, ['Date']);
    stubSignedIn();
  });

  it('baixa o PDF da cobrança com o nome que a API sugeriu e avisa a unidade', () => {
    stubPayments();
    stubInvoice(CHARGE_101.chargeId, 'fatura-2026-11-a-101.pdf');
    cy.visit(PAYMENTS_PATH);

    cy.get('button[aria-label="Baixar fatura de Bloco A · 101"]').click();

    cy.wait('@invoice')
      .its('request.headers.authorization')
      .should('match', /^Bearer /);
    cy.get('[data-testid="notice"]').should('contain.text', 'Fatura de Bloco A · 101 baixada');
    cy.readFile('cypress/downloads/fatura-2026-11-a-101.pdf').should('contain', '%PDF-1.7');
  });

  it('explica quando a cobrança não existe mais, sem baixar nada', () => {
    stubPayments();
    stubInvoiceRejected(CHARGE_102.chargeId, 404, 'finance.charge.not_found');
    cy.visit(PAYMENTS_PATH);

    cy.get('button[aria-label="Baixar fatura de Bloco A · 102"]').click();

    cy.get('[data-testid="invoice-error"]').should('contain.text', 'Esta cobrança não existe mais');
    cy.get('[data-testid="notice"]').should('not.exist');
    cy.get('button[aria-label="Baixar fatura de Bloco A · 102"]').should('be.enabled');
  });

  it('explica o limite de requisições com o tempo de espera', () => {
    stubPayments();
    cy.intercept('GET', `/api/finance/charges/${CHARGE_101.chargeId}/invoice`, {
      statusCode: 429,
      headers: { 'content-type': 'application/problem+json', 'retry-after': '20' },
      body: JSON.stringify({ status: 429, code: 'rate_limit.exceeded' }),
    });
    cy.visit(PAYMENTS_PATH);

    cy.get('button[aria-label="Baixar fatura de Bloco A · 101"]').click();

    cy.get('[data-testid="invoice-error"]').should('contain.text', 'Aguarde 20 segundos');
  });
});
