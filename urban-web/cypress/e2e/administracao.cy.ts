import {
  ANA,
  CARLOS,
  MANAGERS_PATH,
  OVERVIEW_PATH,
  PAULA,
  manyManagers,
  stubChangeRejected,
  stubManagers,
} from '../support/administracao';
import { chooseMenu } from '../support/menu';
import { DESKTOP } from '../support/navegacao';
import { ADMIN } from '../support/pedidos';
import { MANAGER, stubAnonymous, stubSignedIn } from '../support/session';

function rows(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="manager-row"]');
}

describe('Visão geral do administrador', () => {
  beforeEach(() => {
    stubSignedIn(ADMIN);
    stubManagers();
  });

  it('é a tela inicial do administrador: o painel de condomínio leva a ela', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit('/dashboard');

    cy.location('pathname').should('eq', OVERVIEW_PATH);
    cy.contains('h1', 'Visão geral').should('be.visible');
  });

  it('mostra os números da plataforma e leva aos pedidos e aos síndicos', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit(OVERVIEW_PATH);

    cy.get('[data-testid="stat-active"]')
      .should('contain.text', 'Síndicos ativos')
      .and('contain.text', '12');
    cy.get('[data-testid="stat-suspended"]').should('contain.text', '2');
    cy.get('[data-testid="stat-awaiting"]').should('contain.text', '3');
    cy.get('[data-testid="stat-requests"]').should('contain.text', '4');
    cy.get('[data-testid="stat-condominiums"]').should('contain.text', '11');
    cy.get('[data-testid="go-requests"]').should('contain.text', '4 pedidos aguardando análise');

    cy.get('[data-testid="go-managers"]').click();

    cy.location('pathname').should('eq', MANAGERS_PATH);
  });

  it('abre pelo menu lateral', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit('/conta');

    cy.get('[data-testid="nav-overview"]').click();

    cy.location('pathname').should('eq', OVERVIEW_PATH);
  });
});

describe('Síndicos', () => {
  beforeEach(() => {
    stubSignedIn(ADMIN);
  });

  it('abre pelo menu lateral e lista os síndicos com a situação', () => {
    stubManagers([ANA, CARLOS, PAULA]);
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit(OVERVIEW_PATH);

    cy.get('[data-testid="nav-managers"]').click();

    cy.location('pathname').should('eq', MANAGERS_PATH);
    cy.contains('h1', 'Síndicos').should('be.visible');
    rows().should('have.length', 3);
    rows()
      .eq(0)
      .should('contain.text', 'Ana')
      .and('contain.text', 'ana@example.com')
      .and('contain.text', 'Residencial Aurora')
      .and('contain.text', 'Ativo')
      .and('contain.text', '01/11/2026');
    rows()
      .eq(1)
      .should('contain.text', 'Suspenso')
      .and('contain.text', 'Motivo: Uso indevido da plataforma');
    rows().eq(2).should('contain.text', 'Aguardando confirmação');
    cy.get('[data-testid="total"]').should('contain.text', '3 síndicos');
  });

  it('filtra pela situação e pela busca, e limpa os filtros', () => {
    stubManagers();
    cy.visit(MANAGERS_PATH);
    cy.wait('@managers');
    rows().should('have.length', 2);

    chooseMenu('[data-testid="filter-status"]', 'Suspensos');
    cy.wait('@managers').its('request.url').should('contain', 'status=suspended');
    rows().should('have.length', 1).and('contain.text', 'Carlos');

    cy.get('[data-testid="filter-search"]').focus().type('zzz').blur();
    cy.get('[data-testid="no-results"]').should('contain.text', 'Nenhum síndico encontrado');

    cy.get('[data-testid="clear-filters"]').click();
    rows().should('have.length', 2);
    cy.get('[data-testid="clear-filters"]').should('not.exist');
  });

  it('suspende só com motivo, mostra o motivo na lista e avisa', () => {
    stubManagers();
    cy.visit(MANAGERS_PATH);
    rows().should('have.length', 2);

    cy.get('button[aria-label="Suspender Ana"]').click();
    cy.get('urban-suspend-manager-dialog').should('contain.text', 'Suspender Ana');
    cy.get('urban-suspend-manager-dialog button[type="submit"]').click();
    cy.get('urban-suspend-manager-dialog mat-error').should(
      'contain.text',
      'Informe o motivo da suspensão',
    );
    cy.get('@change.all').should('have.length', 0);

    cy.get('urban-suspend-manager-dialog textarea').type('Inadimplência com a plataforma');
    cy.get('urban-suspend-manager-dialog button[type="submit"]').click();

    cy.wait('@change')
      .its('request.body')
      .should('deep.equal', { reason: 'Inadimplência com a plataforma' });
    cy.get('urban-suspend-manager-dialog').should('not.exist');
    cy.get('[data-testid="notice"]').should('contain.text', 'Acesso de Ana suspenso');
    rows()
      .eq(0)
      .should('contain.text', 'Suspenso')
      .and('contain.text', 'Motivo: Inadimplência com a plataforma');
    cy.get('button[aria-label="Reativar Ana"]').should('be.visible');
  });

  it('desiste da suspensão sem chamar a API', () => {
    stubManagers();
    cy.visit(MANAGERS_PATH);
    rows().should('have.length', 2);

    cy.get('button[aria-label="Suspender Ana"]').click();
    cy.get('urban-suspend-manager-dialog [data-testid="cancel"]').click();

    cy.get('urban-suspend-manager-dialog').should('not.exist');
    cy.get('@change.all').should('have.length', 0);
  });

  it('reativa com um clique e avisa', () => {
    stubManagers();
    cy.visit(MANAGERS_PATH);
    rows().should('have.length', 2);

    cy.get('button[aria-label="Reativar Carlos"]').click();

    cy.wait('@change').its('request.url').should('contain', `${CARLOS.id}/reactivate`);
    cy.get('[data-testid="notice"]').should('contain.text', 'Acesso de Carlos reativado');
    rows().eq(1).should('contain.text', 'Ativo').and('not.contain.text', 'Motivo');
  });

  it('explica no diálogo quando a situação já mudou e atualiza a lista', () => {
    stubChangeRejected(409, 'identity.user.invalid_transition', [CARLOS]);
    cy.visit(MANAGERS_PATH);
    rows().should('have.length', 1);

    cy.get('button[aria-label="Reativar Carlos"]').click();

    cy.wait('@change');
    cy.get('[data-testid="action-error"]').should(
      'contain.text',
      'A situação deste síndico mudou. A lista foi atualizada',
    );
  });

  it('pagina a lista de 25 síndicos', () => {
    stubManagers(manyManagers(25));
    cy.visit(MANAGERS_PATH);

    rows().should('have.length', 20);
    cy.get('[data-testid="page-status"]').should('contain.text', 'Página 1 de 2');

    cy.get('[data-testid="next"]').click();

    rows().should('have.length', 5);
    cy.get('[data-testid="page-status"]').should('contain.text', 'Página 2 de 2');
    cy.get('[data-testid="next"]').should('be.disabled');
  });

  it('leva o síndico de volta ao painel', () => {
    stubSignedIn(MANAGER);
    cy.visit(MANAGERS_PATH);

    cy.location('pathname').should('eq', '/dashboard');
  });

  it('pede a entrada de quem não está logado e volta aos síndicos depois', () => {
    stubAnonymous();
    cy.visit(MANAGERS_PATH);

    cy.location('pathname').should('eq', '/entrar');
    cy.location('search').should('contain', 'returnUrl=%2Fadmin%2Fsindicos');
  });
});
