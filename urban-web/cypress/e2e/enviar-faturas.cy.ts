import { TODAY, stubMonth } from '../support/financeiro';
import {
  INVOICE_DELIVERIES_PATH,
  stubInvoiceDeliveries,
  stubInvoiceDeliveriesNotClosed,
  stubResend,
  stubResendRejected,
  stubSendMonth,
} from '../support/enviar-faturas';
import { RESIDENTS_PATH, stubResidents } from '../support/moradores';
import { DESKTOP } from '../support/navegacao';
import { stubSignedIn } from '../support/session';

/** A entrega simulada leva duas leituras da lista, e a tela lê de 5 em 5 segundos. */
const DELIVERY_WAIT = { timeout: 20_000 } as const;

function rows(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="delivery-row"]');
}

describe('Enviar faturas', () => {
  beforeEach(() => {
    cy.clock(TODAY, ['Date']);
    stubSignedIn();
  });

  it('abre pelo menu lateral, no mês de hoje, com as contagens e uma linha por unidade', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    stubInvoiceDeliveries();
    stubMonth();
    cy.visit('/dashboard');

    cy.get('[data-testid="nav-invoice-deliveries"]').click();

    cy.location('pathname').should('eq', INVOICE_DELIVERIES_PATH);
    cy.contains('h1', 'Enviar faturas').should('be.visible');
    cy.get('[data-testid="month-title"]').should('contain.text', 'Novembro de 2026');
    cy.get('[data-testid="count-sent"]').should('contain.text', 'Enviadas');
    cy.get('[data-testid="count-failed"]').should('contain.text', 'Falharam');
    cy.get('[data-testid="pending"]').should('contain.text', '2 faturas a enviar');
    rows().should('have.length', 4);
    rows().first().should('contain.text', 'Bloco A · 101').and('contain.text', 'Não enviada');
    rows()
      .eq(1)
      .should('contain.text', 'Falhou')
      .and('contain.text', 'O provedor de e-mail recusou o envio');
    rows().eq(2).should('contain.text', 'Enviada').and('contain.text', '12/11/2026 às 10:30');
    rows().eq(3).should('contain.text', 'Sem e-mail');
  });

  it('explica que o mês não foi fechado e leva aos lançamentos do mês', () => {
    stubInvoiceDeliveriesNotClosed();
    stubMonth();
    cy.visit(INVOICE_DELIVERIES_PATH);

    cy.get('[data-testid="not-closed"]').should(
      'contain.text',
      'Novembro de 2026 ainda não foi fechado',
    );
    cy.get('[data-testid="close-link"]').click();

    cy.location('pathname').should('eq', '/financeiro/lancamentos');
    cy.location('search').should('eq', '?mes=2026-11');
  });

  it('leva aos moradores quando falta o e-mail do responsável', () => {
    stubInvoiceDeliveries();
    stubResidents();
    cy.visit(INVOICE_DELIVERIES_PATH);

    cy.get('[data-testid="add-email"]').click();

    cy.location('pathname').should('eq', RESIDENTS_PATH);
  });

  describe('envio do mês', () => {
    it('pede confirmação, põe as faturas na fila e acompanha até serem enviadas', () => {
      const deliveries = stubInvoiceDeliveries();
      stubSendMonth(deliveries);
      cy.visit(INVOICE_DELIVERIES_PATH);
      rows().should('have.length', 4);

      cy.get('[data-testid="send-month"]').click();
      cy.get('[data-testid="send-confirm"]').should(
        'contain.text',
        'Enviar 2 faturas de Novembro de 2026',
      );
      cy.get('[data-testid="confirm-send"]').click();

      cy.wait('@sendMonth');
      cy.get('[data-testid="notice"]')
        .should('contain.text', '2 faturas entraram na fila de envio')
        .and('contain.text', '1 unidade já tinha recebido a fatura')
        .and('contain.text', '1 unidade ficou de fora por não ter responsável com e-mail');
      rows().first().should('contain.text', 'Na fila');
      cy.get('[data-testid="send-confirm"]').should('not.exist');

      rows().first().should('contain.text', 'Enviada', DELIVERY_WAIT);
      rows().eq(1).should('contain.text', 'Enviada');
      cy.get('[data-testid="count-queued"]').should('contain.text', '0');
      cy.get('[data-testid="pending"]').should('contain.text', 'Nenhuma fatura a enviar');
      cy.get('[data-testid="send-month"]').should('be.disabled');
    });

    it('cancela a confirmação sem mandar nada', () => {
      const deliveries = stubInvoiceDeliveries();
      stubSendMonth(deliveries);
      cy.visit(INVOICE_DELIVERIES_PATH);

      cy.get('[data-testid="send-month"]').click();
      cy.get('[data-testid="cancel-send"]').click();

      cy.get('[data-testid="send-confirm"]').should('not.exist');
      rows().first().should('contain.text', 'Não enviada');
    });
  });

  describe('envio de uma unidade', () => {
    it('reenvia a fatura de uma unidade e mostra que entrou na fila', () => {
      const deliveries = stubInvoiceDeliveries();
      stubResend(deliveries);
      cy.visit(INVOICE_DELIVERIES_PATH);

      cy.get('button[aria-label="Enviar de novo fatura de Bloco A · 102"]').click();

      cy.wait('@resend');
      cy.get('[data-testid="notice"]').should(
        'contain.text',
        'Fatura de Bloco A · 102 entrou na fila de envio',
      );
      rows().eq(1).should('contain.text', 'Na fila');
    });

    it('explica quando a unidade ficou sem e-mail', () => {
      stubInvoiceDeliveries();
      stubResendRejected(422, 'finance.invoice_delivery.no_recipient');
      cy.visit(INVOICE_DELIVERIES_PATH);

      cy.get('button[aria-label="Enviar fatura de Bloco A · 101"]').click();

      cy.get('[data-testid="action-error"]').should(
        'contain.text',
        'Esta unidade não tem responsável com e-mail cadastrado',
      );
    });
  });
});
