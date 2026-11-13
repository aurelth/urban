import { stubSummary } from '../support/administracao';
import { PALETTE } from '../support/layout';
import { DESKTOP } from '../support/navegacao';
import {
  ADMIN,
  FIRST_REQUEST,
  REQUESTS_PATH,
  SECOND_REQUEST,
  stubDecisionRejected,
  stubRequests,
} from '../support/pedidos';
import { MANAGER, stubAnonymous, stubSignedIn } from '../support/session';

function requests(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="request"]');
}

describe('Pedidos de cadastro', () => {
  beforeEach(() => {
    stubSignedIn(ADMIN);
    stubSummary();
    stubRequests();
  });

  it('abre pelo menu lateral do administrador e lista os pedidos', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit('/dashboard');

    cy.get('[data-testid="nav-registration-requests"]').click();

    cy.location('pathname').should('eq', REQUESTS_PATH);
    cy.contains('h1', 'Pedidos de cadastro').should('be.visible');
    requests().should('have.length', 2);
    requests()
      .first()
      .should('contain.text', 'Marina')
      .and('contain.text', 'marina@example.com')
      .and('contain.text', 'Residencial Aurora')
      .and('contain.text', 'Recebido em 13/11/2026 às 01:23');
  });

  it('aprova um pedido, tira da lista e avisa que o link foi enviado', () => {
    cy.visit(REQUESTS_PATH);
    requests().should('have.length', 2);

    requests().first().find('[data-testid="approve"]').click();

    cy.wait('@decision').its('request.url').should('contain', `${FIRST_REQUEST.id}/approve`);
    requests().should('have.length', 1).and('contain.text', 'Carlos');
    cy.get('[data-testid="notice"]')
      .should('contain.text', 'Pedido de Marina aprovado')
      .and('contain.text', 'marina@example.com')
      .find('.text-success mat-icon')
      .should('have.css', 'color', PALETTE.light.success);
  });

  it('só recusa depois da confirmação, e avisa que nada foi enviado', () => {
    cy.visit(REQUESTS_PATH);
    requests().should('have.length', 2);

    requests().first().find('[data-testid="reject"]').click();
    cy.get('[data-testid="reject-confirmation"]')
      .should('be.visible')
      .and('contain.text', 'A recusa é final');
    cy.get('@decision.all').should('have.length', 0);

    cy.get('[data-testid="confirm-reject"]').click();

    cy.wait('@decision').its('request.url').should('contain', `${FIRST_REQUEST.id}/reject`);
    requests().should('have.length', 1).and('contain.text', 'Carlos');
    cy.get('[data-testid="notice"]').should('contain.text', 'Pedido de Marina recusado');
    cy.get('[data-testid="reject-confirmation"]').should('not.exist');
  });

  it('desiste da recusa sem chamar a API', () => {
    cy.visit(REQUESTS_PATH);
    requests().should('have.length', 2);

    requests().first().find('[data-testid="reject"]').click();
    cy.get('[data-testid="cancel-reject"]').click();

    cy.get('[data-testid="reject-confirmation"]').should('not.exist');
    requests().should('have.length', 2);
    cy.get('@decision.all').should('have.length', 0);
  });

  it('mostra o estado vazio quando não há pedidos', () => {
    stubRequests([]);
    cy.visit(REQUESTS_PATH);

    cy.get('[data-testid="empty"]').should('contain.text', 'Nenhum pedido aguardando análise');
  });

  it('explica quando o pedido já foi decidido e atualiza a lista', () => {
    stubDecisionRejected(409, 'identity.user.invalid_transition', [SECOND_REQUEST]);
    cy.visit(REQUESTS_PATH);
    requests().should('have.length', 1);

    requests().first().find('[data-testid="approve"]').click();

    cy.wait('@decision');
    cy.get('[data-testid="action-error"]').should(
      'contain.text',
      'Este pedido já foi decidido. A lista foi atualizada',
    );
  });

  it('leva o síndico de volta ao painel', () => {
    stubSignedIn(MANAGER);
    cy.visit(REQUESTS_PATH);

    cy.location('pathname').should('eq', '/dashboard');
  });

  it('pede a entrada de quem não está logado e volta aos pedidos depois', () => {
    stubAnonymous();
    cy.visit(REQUESTS_PATH);

    cy.location('pathname').should('eq', '/entrar');
    cy.location('search').should('contain', 'returnUrl=%2Fadmin%2Fpedidos-de-cadastro');
  });
});
