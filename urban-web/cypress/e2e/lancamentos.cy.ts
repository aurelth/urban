import {
  EMPTY_MONTH,
  ENTRIES_PATH,
  OPENED_MONTH,
  PENDING_ENTRY,
  TODAY,
  stubFinanceRejected,
  stubMonth,
} from '../support/financeiro';
import { PALETTE } from '../support/layout';
import { stubSignedIn } from '../support/session';

describe('Lançamentos do mês', () => {
  beforeEach(() => {
    cy.clock(TODAY, ['Date']);
    stubSignedIn();
  });

  it('abre no mes atual com o total, a pendencia e os grupos', () => {
    stubMonth();
    cy.visit(ENTRIES_PATH);

    cy.contains('h1', 'Lançamentos do mês').should('be.visible');
    cy.get('[data-testid="month-title"]').should('contain.text', 'Novembro de 2026');
    cy.get('[data-testid="total"]').should('contain.text', '4.200,00');
    cy.get('[data-testid="pending-count"]')
      .should('contain.text', '1 lançamento pendente')
      .find('.text-warning mat-icon')
      .should('have.css', 'color', PALETTE.light.warning);
    cy.get('[data-testid="group-section"] h2').should('contain.text', 'Despesas ordinárias');
    cy.get('[data-testid="entry-row"]').should('have.length', 2);
    cy.get('[data-testid="entry-row"]')
      .eq(1)
      .should('contain.text', 'Pendente')
      .and('contain.text', 'sugestão')
      .and('contain.text', '812,40');
  });

  it('abre um mes que ainda nao tem lancamentos', () => {
    stubMonth(EMPTY_MONTH);
    cy.intercept('POST', '/api/finance/months/2026-11/open', {
      statusCode: 200,
      body: OPENED_MONTH,
    }).as('openMonth');
    cy.visit(ENTRIES_PATH);

    cy.get('[data-testid="empty"]').should('be.visible');
    cy.get('[data-testid="open-month"]').should('contain.text', 'Abrir mês').click();

    cy.wait('@openMonth');
    cy.get('[data-testid="empty"]').should('not.exist');
    cy.get('[data-testid="entry-row"]').should('have.length', 2);
    cy.get('[data-testid="notice"]').should('contain.text', 'Mês aberto');
  });

  it('explica quando a API recusa abrir o mes', () => {
    stubMonth(EMPTY_MONTH);
    stubFinanceRejected(
      'POST',
      '/api/finance/months/2026-11/open',
      422,
      'month.too_far_ahead',
      'openMonth',
    );
    cy.visit(ENTRIES_PATH);

    cy.get('[data-testid="open-month"]').click();

    cy.get('[data-testid="action-error"]').should(
      'contain.text',
      'Só é possível abrir o mês atual, os anteriores e o mês seguinte',
    );
  });

  it('informa o valor de um lancamento pendente com a sugestao do mes anterior', () => {
    stubMonth();
    cy.intercept('PUT', `/api/finance/entries/${PENDING_ENTRY.id}/amount`, {
      statusCode: 200,
      body: { ...PENDING_ENTRY, amount: 830.1, isPending: false },
    }).as('setAmount');
    cy.visit(ENTRIES_PATH);

    cy.get('button[aria-label="Informar valor de Conta de luz"]').click();
    cy.get('urban-entry-amount-dialog [formControlName="amount"]').should('have.value', '812,40');
    cy.get('urban-entry-amount-dialog [formControlName="amount"]')
      .click('right')
      .type('{selectall}830,10');
    cy.get('urban-entry-amount-dialog').contains('button', 'Informar valor').click();

    cy.wait('@setAmount')
      .its('request.body')
      .should('deep.equal', { version: '22', amount: 830.1 });
    cy.get('urban-entry-amount-dialog').should('not.exist');
    cy.get('[data-testid="notice"]').should('contain.text', 'Valor informado');
  });

  it('navega entre os meses e nao passa do mes seguinte', () => {
    stubMonth();
    cy.visit(ENTRIES_PATH);
    cy.wait('@month').its('request.url').should('contain', '/months/2026-11');

    cy.get('button[aria-label="Mês anterior"]').click();
    cy.wait('@month').its('request.url').should('contain', '/months/2026-10');
    cy.get('[data-testid="month-title"]').should('contain.text', 'Outubro de 2026');

    cy.get('button[aria-label="Próximo mês"]').click();
    cy.get('button[aria-label="Próximo mês"]').click();
    cy.get('[data-testid="month-title"]').should('contain.text', 'Dezembro de 2026');
    cy.get('button[aria-label="Próximo mês"]').should('be.disabled');
  });

  it('volta para as despesas pelo link', () => {
    stubMonth();
    cy.visit(ENTRIES_PATH);

    cy.get('[data-testid="expenses-link"]').click();

    cy.location('pathname').should('eq', '/financeiro/despesas');
  });

  it('cabe numa tela de 360 px, sem rolagem horizontal da pagina', () => {
    cy.viewport(360, 640);
    stubMonth();
    cy.visit(ENTRIES_PATH);

    cy.get('[data-testid="entry-row"]').should('have.length', 2);
    cy.document().should((doc) => {
      expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
    });
  });
});
