import { TODAY, stubMonth } from '../support/financeiro';
import {
  DEBTOR_102,
  DELINQUENCY_PATH,
  stubDelinquency,
  stubResendInvoice,
} from '../support/inadimplencia';
import { stubResendRejected } from '../support/enviar-faturas';
import { DESKTOP } from '../support/navegacao';
import { stubPayments } from '../support/pagamentos';
import { stubSignedIn } from '../support/session';

function debtors(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="debtor"]');
}

describe('Inadimplência', () => {
  beforeEach(() => {
    cy.clock(TODAY, ['Date']);
    stubSignedIn();
  });

  it('abre pelo menu lateral, com o total em atraso e uma seção por unidade devedora', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    stubDelinquency();
    stubPayments();
    stubMonth();
    cy.visit('/dashboard');

    cy.get('[data-testid="nav-delinquency"]').click();

    cy.location('pathname').should('eq', DELINQUENCY_PATH);
    cy.contains('h1', 'Inadimplência').should('be.visible');
    cy.get('[data-testid="stat-total"]')
      .invoke('text')
      .should('match', /R\$\s2\.500,00/);
    cy.get('[data-testid="stat-units"]').should('contain.text', '3');
    cy.get('[data-testid="as-of"]').should('contain.text', 'Situação em 15/11/2026');
    debtors().should('have.length', 3);
    debtors().first().should('contain.text', 'Bloco A · 101');
    debtors()
      .first()
      .find('[data-testid="debtor-balance"]')
      .invoke('text')
      .should('match', /R\$\s1\.300,00/);
    debtors().eq(1).should('contain.text', 'Bloco A · 102');
    debtors().eq(2).should('contain.text', '201');
  });

  it('mostra as cobranças em atraso de cada unidade, com valor, pago, saldo e dias de atraso', () => {
    stubDelinquency();
    cy.visit(DELINQUENCY_PATH);

    debtors()
      .first()
      .find('[data-testid="debtor-summary"]')
      .should(
        'contain.text',
        '2 cobranças em atraso · a mais antiga venceu em 10/10/2026, há 36 dias',
      );
    debtors().first().find('[data-testid="charge-row"]').should('have.length', 2);
    debtors()
      .first()
      .find('[data-testid="charge-row"]')
      .first()
      .should('contain.text', 'Setembro de 2026')
      .and('contain.text', '10/10/2026')
      .and('contain.text', '36 dias');
    debtors()
      .first()
      .find('[data-testid="charge-row"]')
      .eq(1)
      .should('contain.text', 'Outubro de 2026')
      .and('contain.text', '5 dias');
  });

  it('diz que nenhuma unidade está em atraso quando todas pagaram', () => {
    stubDelinquency([]);
    cy.visit(DELINQUENCY_PATH);

    cy.get('[data-testid="empty"]').should('contain.text', 'Nenhuma unidade em atraso');
    cy.get('[data-testid="stat-total"]')
      .invoke('text')
      .should('match', /R\$\s0,00/);
    debtors().should('not.exist');
  });

  it('leva aos pagamentos, onde se registra o que foi pago', () => {
    stubDelinquency();
    stubPayments();
    stubMonth();
    cy.visit(DELINQUENCY_PATH);

    cy.get('[data-testid="payments-link"]').click();

    cy.location('pathname').should('eq', '/financeiro/pagamentos');
  });

  describe('reenvio da fatura', () => {
    it('põe a fatura da cobrança na fila e avisa', () => {
      stubDelinquency();
      stubResendInvoice();
      cy.visit(DELINQUENCY_PATH);

      cy.get('button[aria-label="Reenviar fatura de Outubro de 2026 de Bloco A · 101"]').click();

      cy.wait('@resend')
        .its('request.url')
        .should(
          'contain',
          '/api/finance/charges/0199bbbb-0030-7000-8000-000000000101/invoice-deliveries',
        );
      cy.get('[data-testid="notice"]').should(
        'contain.text',
        'Fatura de Bloco A · 101 (Outubro de 2026) entrou na fila de envio',
      );
    });

    it('explica quando a unidade não tem e-mail para receber a fatura', () => {
      stubDelinquency();
      stubResendRejected(422, 'finance.invoice_delivery.no_recipient');
      cy.visit(DELINQUENCY_PATH);

      cy.get('button[aria-label="Reenviar fatura de Outubro de 2026 de 201"]').click();

      cy.get('[data-testid="action-error"]').should(
        'contain.text',
        'Esta unidade não tem responsável com e-mail cadastrado',
      );
    });
  });

  describe('muitas unidades', () => {
    it('pagina pelo total de unidades devedoras', () => {
      const many = Array.from({ length: 51 }, (_, index) => ({
        ...DEBTOR_102,
        unitId: `0199bbbb-0005-7000-8000-${String(index).padStart(12, '0')}`,
        number: String(100 + index),
      }));
      stubDelinquency(many);
      cy.visit(DELINQUENCY_PATH);

      debtors().should('have.length', 50);
      cy.get('[data-testid="page-status"]').should('contain.text', 'Página 1 de 2');

      cy.get('[data-testid="next"]').click();

      debtors().should('have.length', 1);
      cy.get('[data-testid="page-status"]').should('contain.text', 'Página 2 de 2');
    });
  });
});
