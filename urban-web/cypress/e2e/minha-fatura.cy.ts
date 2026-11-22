import {
  DEPENDENT_MINE,
  MY_INVOICE_PATH,
  NO_UNIT_MINE,
  OVERDUE_CHARGE_ID,
  PAID_CHARGE_ID,
  stubMyInvoice,
  stubMyInvoiceRejected,
  stubMyMonth,
} from '../support/minha-fatura';
import { TODAY } from '../support/financeiro';
import { DESKTOP } from '../support/navegacao';
import { MANAGER, RESIDENT, stubSignedIn } from '../support/session';

function text(testId: string): Cypress.Chainable<string> {
  return cy.get(`[data-testid="${testId}"]`).invoke('text');
}

describe('Minha fatura', () => {
  beforeEach(() => {
    cy.clock(TODAY, ['Date']);
    stubSignedIn(RESIDENT);
    stubMyMonth();
  });

  it('abre pelo menu lateral do morador, no mês de hoje', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit('/dashboard');

    cy.get('[data-testid="nav-my-invoice"]').should('contain.text', 'Minha fatura').click();

    cy.location('pathname').should('eq', MY_INVOICE_PATH);
    cy.contains('h1', 'Minha fatura').should('be.visible');
    cy.get('[data-testid="month-title"]').should('contain.text', 'Novembro de 2026');
  });

  it('abre pelo atalho do painel do morador', () => {
    cy.visit('/dashboard');

    cy.get('[data-testid="my-invoice-link"]').click();

    cy.location('pathname').should('eq', MY_INVOICE_PATH);
  });

  it('mostra um cartão por unidade, com valor, pago, saldo e a situação em texto', () => {
    cy.visit(MY_INVOICE_PATH);

    cy.get('[data-testid="unit-card"]').should('have.length', 2);
    cy.get('[data-testid="unit-heading"]').first().should('have.text', 'Unidade 101');
    cy.get('[data-testid="unit-heading"]').last().should('have.text', 'Bloco A · Unidade 102');
    cy.get('[data-testid="unit-card"]')
      .first()
      .within(() => {
        cy.get('[data-testid="due-date"]').should('contain.text', '10/11/2026');
        text('amount').should('match', /R\$\s800,00/);
        text('paid').should('match', /R\$\s300,00/);
        text('balance').should('match', /R\$\s500,00/);
        cy.get('[data-testid="status"]').should('contain.text', 'Atrasada');
        cy.get('[data-testid="last-received"]').should('contain.text', '05/11/2026');
      });
    cy.get('[data-testid="unit-card"]')
      .last()
      .find('[data-testid="status"]')
      .should('contain.text', 'Paga');
    cy.get('[data-testid="as-of"]').should('contain.text', 'Situação em 15/11/2026');
  });

  it('mostra as despesas do mês por grupo, sem o que o prédio recebeu', () => {
    cy.visit(MY_INVOICE_PATH);

    text('expenses-total').should('match', /R\$\s3\.000,00/);
    cy.get('[data-testid="group"]').should('have.length', 2);
    cy.get('[data-testid="group"]').first().should('contain.text', '60%');
    cy.get('body').should('not.contain.text', 'nadimpl');
  });

  it('baixa o PDF da própria fatura e avisa com a unidade', () => {
    stubMyInvoice(OVERDUE_CHARGE_ID, 'fatura-2026-11-101.pdf');
    cy.visit(MY_INVOICE_PATH);

    cy.get('[data-testid="download-invoice"]').first().click();

    cy.wait('@myInvoice');
    cy.get('[data-testid="notice"]').should('contain.text', 'Fatura de Unidade 101 baixada');
  });

  it('explica quando a API recusa a fatura', () => {
    stubMyInvoiceRejected(PAID_CHARGE_ID, 404, 'finance.charge.not_found');
    cy.visit(MY_INVOICE_PATH);

    cy.get('[data-testid="download-invoice"]').last().click();

    cy.wait('@myInvoice');
    cy.get('[data-testid="invoice-error"]').should(
      'contain.text',
      'Esta fatura não está disponível',
    );
  });

  it('navega para o mês seguinte, ainda aberto, e explica que a fatura aparece depois do fechamento', () => {
    cy.visit(MY_INVOICE_PATH);

    cy.get('[data-testid="next-month"]').click();

    cy.get('[data-testid="month-title"]').should('contain.text', 'Dezembro de 2026');
    cy.get('[data-testid="not-closed"]').should('contain.text', 'ainda não foi fechada');
    cy.get('[data-testid="unit-card"]').should('not.exist');
    cy.get('[data-testid="card-expenses"]').should('not.exist');
  });

  it('abre no mês do endereço (?mes=)', () => {
    cy.visit(`${MY_INVOICE_PATH}?mes=2026-12`);

    cy.get('[data-testid="month-title"]').should('contain.text', 'Dezembro de 2026');
  });

  it('dependente: explica que a fatura fica com o responsável pela unidade', () => {
    stubMyMonth({ '2026-11': DEPENDENT_MINE });
    cy.visit(MY_INVOICE_PATH);

    cy.get('[data-testid="dependent-notice"]').should(
      'contain.text',
      'A fatura fica com o proprietário ou o inquilino da unidade',
    );
  });

  it('sem unidade: pede para falar com o síndico', () => {
    stubMyMonth({ '2026-11': NO_UNIT_MINE });
    cy.visit(MY_INVOICE_PATH);

    cy.get('[data-testid="no-unit-notice"]').should('contain.text', 'Fale com o síndico');
  });

  it('o síndico também abre a tela pelo endereço, que é a fatura da unidade dele', () => {
    stubSignedIn(MANAGER);
    cy.visit(MY_INVOICE_PATH);

    cy.contains('h1', 'Minha fatura').should('be.visible');
    cy.get('[data-testid="unit-card"]').should('have.length', 2);
  });
});
