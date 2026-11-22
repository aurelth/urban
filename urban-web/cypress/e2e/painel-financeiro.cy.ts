import { TODAY } from '../support/financeiro';
import { DESKTOP } from '../support/navegacao';
import {
  MONTH_DASHBOARD_PATH,
  failMonthDashboardOnce,
  stubMonthDashboard,
} from '../support/painel-financeiro';
import { stubSignedIn } from '../support/session';

function text(testId: string): Cypress.Chainable<string> {
  return cy.get(`[data-testid="${testId}"]`).invoke('text');
}

describe('Painel financeiro', () => {
  beforeEach(() => {
    cy.clock(TODAY, ['Date']);
    stubSignedIn();
    stubMonthDashboard();
  });

  it('abre pelo menu lateral, no mês de hoje, com o título e o mês', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit('/dashboard');

    cy.get('[data-testid="nav-month-dashboard"]').click();

    cy.location('pathname').should('eq', MONTH_DASHBOARD_PATH);
    cy.contains('h1', 'Painel financeiro').should('be.visible');
    cy.get('[data-testid="month-title"]').should('contain.text', 'Novembro de 2026');
  });

  it('mostra as despesas do mês por grupo, com o valor e a parte do total', () => {
    cy.visit(MONTH_DASHBOARD_PATH);

    text('expenses-total').should('match', /R\$\s3\.000,00/);
    cy.get('[data-testid="group"]').should('have.length', 2);
    cy.get('[data-testid="group"]')
      .first()
      .should('contain.text', 'Despesas ordinárias')
      .and('contain.text', '60%');
    cy.get('[data-testid="group"]')
      .last()
      .should('contain.text', 'Obras')
      .and('contain.text', '40%');
  });

  it('mostra o que foi cobrado, recebido e a receber, e as unidades de cada situação', () => {
    cy.visit(MONTH_DASHBOARD_PATH);

    cy.get('[data-testid="due-date"]').should('contain.text', '10/11/2026');
    text('received').should('match', /R\$\s1\.300,00/);
    cy.get('[data-testid="received-percent"]').should('contain.text', '54% recebido');
    text('to-receive').should('match', /A receber\s+R\$\s1\.100,00/);
    cy.get('[data-testid="situation-paid"]').should('contain.text', 'Pagas');
    cy.get('[data-testid="situation-partial"]').should('contain.text', 'Parciais');
    cy.get('[data-testid="situation-overdue"]').should('contain.text', 'Atrasadas');
    cy.get('[data-testid="as-of"]').should('contain.text', 'Situação em 15/11/2026');
  });

  it('destaca o saldo atrasado e leva à inadimplência', () => {
    cy.visit(MONTH_DASHBOARD_PATH);

    cy.get('[data-testid="charges-overdue"]').should('contain.text', '1 cobrança em atraso');
    text('overdue-amount').should('match', /R\$\s800,00 em atraso neste mês/);
    cy.get('[data-testid="delinquency-link"]').should(
      'have.attr',
      'href',
      '/financeiro/inadimplencia',
    );
  });

  it('vai ao mês anterior e ao próximo pelas setas, e cada mês mostra o seu painel', () => {
    cy.visit(MONTH_DASHBOARD_PATH);
    cy.get('[data-testid="received"]').should('exist');

    cy.get('[data-testid="previous-month"]').click();

    cy.get('[data-testid="month-title"]').should('contain.text', 'Outubro de 2026');
    cy.get('[data-testid="charges-overdue"]').should('contain.text', 'Nenhuma cobrança em atraso');
    cy.get('[data-testid="overdue-amount"]').should('not.exist');
    cy.get('[data-testid="delinquency-link"]').should('not.exist');

    cy.get('[data-testid="next-month"]').click();
    cy.get('[data-testid="month-title"]').should('contain.text', 'Novembro de 2026');
    cy.get('[data-testid="next-month"]').should('not.be.disabled').click();

    cy.get('[data-testid="month-title"]').should('contain.text', 'Dezembro de 2026');
    cy.get('[data-testid="needs-amount"]').should('contain.text', '2 lançamentos aguardam o valor');
    cy.get('[data-testid="not-closed"]').should(
      'contain.text',
      'As cobranças de Dezembro de 2026 aparecem depois que o mês for fechado',
    );
  });

  it('explica um mês que nunca foi aberto e leva aos lançamentos dele', () => {
    cy.visit(`${MONTH_DASHBOARD_PATH}?mes=2027-03`);

    cy.get('[data-testid="month-title"]').should('contain.text', 'Março de 2027');
    cy.get('[data-testid="no-expenses"]').should('contain.text', 'Nenhuma despesa neste mês');
    cy.get('[data-testid="entries-link"]').should(
      'have.attr',
      'href',
      '/financeiro/lancamentos?mes=2027-03',
    );
  });

  it('abre no mês pedido na barra de endereços e leva aos pagamentos desse mês', () => {
    cy.visit(`${MONTH_DASHBOARD_PATH}?mes=2026-10`);

    cy.get('[data-testid="month-title"]').should('contain.text', 'Outubro de 2026');
    cy.get('[data-testid="payments-link"]')
      .should('have.attr', 'href', '/financeiro/pagamentos?mes=2026-10')
      .click();
    cy.location('pathname').should('eq', '/financeiro/pagamentos');
    cy.location('search').should('eq', '?mes=2026-10');
  });

  it('mostra o erro de leitura e tenta de novo', () => {
    failMonthDashboardOnce();
    cy.visit(MONTH_DASHBOARD_PATH);

    cy.get('[data-testid="load-error"]').should('be.visible');
    cy.get('[data-testid="card-expenses"]').should('not.exist');

    cy.get('[data-testid="retry"]').click();

    cy.get('[data-testid="load-error"]').should('not.exist');
    cy.get('[data-testid="card-expenses"]').should('be.visible');
  });
});
