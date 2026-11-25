import {
  DEPENDENT_MINE,
  FIRST_PAYMENT_ID,
  MY_INVOICE_PATH,
  NO_UNIT_MINE,
  NOVEMBER_MINE,
  OVERDUE_CHARGE_ID,
  PAID_CHARGE_ID,
  PAID_PAYMENT_ID,
  REVERSED_PAYMENT_ID,
  stubMyInvoice,
  stubMyInvoiceRejected,
  stubMyMonth,
  stubMyReceipt,
  stubMyReceiptRejected,
} from '../support/minha-fatura';
import { TODAY } from '../support/financeiro';
import { PALETTE } from '../support/layout';
import { DESKTOP } from '../support/navegacao';
import { stubMural } from '../support/mural';
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
    stubMural();
    cy.visit('/dashboard');

    cy.get('[data-testid="nav-my-invoice"]').should('contain.text', 'Minha fatura').click();

    cy.location('pathname').should('eq', MY_INVOICE_PATH);
    cy.contains('h1', 'Minha fatura').should('be.visible');
    cy.get('[data-testid="month-title"]').should('contain.text', 'Novembro de 2026');
  });

  it('mostra um cartão por unidade, com valor, pago, saldo e a situação em texto', () => {
    cy.visit(MY_INVOICE_PATH);

    cy.get('[data-testid="unit-card"]').should('have.length', 2);
    cy.get('[data-testid="unit-heading"]').first().should('contain.text', 'Unidade 101');
    cy.get('[data-testid="unit-heading"]').last().should('contain.text', 'Bloco A · Unidade 102');
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
    cy.get('[data-testid="download-error"]').should(
      'contain.text',
      'Esta fatura não está disponível',
    );
  });

  describe('pagamentos e recibos', () => {
    it('lista os pagamentos de cada unidade com o código do recibo, e marca o estornado', () => {
      cy.visit(MY_INVOICE_PATH);

      cy.get('[data-testid="unit-card"]')
        .first()
        .within(() => {
          cy.get('[data-testid="payments-heading"]').should('contain.text', 'Pagamentos');
          cy.get('[data-testid="payment-item"]').should('have.length', 3);
          cy.get('[data-testid="payment-info"]').eq(0).should('contain.text', '03/11/2026 · Pix');
          cy.get('[data-testid="payment-info"]')
            .eq(1)
            .should('contain.text', '05/11/2026 · Dinheiro');
          cy.get('[data-testid="receipt-code"]').eq(0).should('contain.text', 'Recibo REC-000001');
          cy.get('[data-testid="receipt-code"]')
            .eq(2)
            .should('contain.text', 'Recibo REC-000004 · cancelado');
          cy.get('[data-testid="reversed"]')
            .should('have.length', 1)
            .and('contain.text', 'Pagamento estornado');
        });
      cy.get('[data-testid="unit-card"]')
        .last()
        .find('[data-testid="payment-item"]')
        .should('have.length', 1)
        .and('contain.text', 'REC-000003');
    });

    it('não mostra pagamentos na unidade sem nenhum', () => {
      const [unit] = NOVEMBER_MINE.units;
      stubMyMonth({
        '2026-11': {
          ...NOVEMBER_MINE,
          units: [{ ...unit, charge: { ...unit.charge, paid: 0, balance: 800, payments: [] } }],
        },
      });
      cy.visit(MY_INVOICE_PATH);

      cy.get('[data-testid="unit-card"]').should('exist');
      cy.get('[data-testid="payment-item"]').should('not.exist');
      cy.get('[data-testid="payments-heading"]').should('not.exist');
    });

    it('baixa o recibo de um pagamento pela rota do morador e avisa com o código', () => {
      stubMyReceipt(FIRST_PAYMENT_ID, 'recibo-000001.pdf');
      cy.visit(MY_INVOICE_PATH);

      cy.get('button[aria-label="Baixar recibo REC-000001"]').click();

      cy.wait('@myReceipt')
        .its('request.url')
        .should('include', `/api/finance/me/payments/${FIRST_PAYMENT_ID}/receipt`);
      cy.get('[data-testid="notice"]').should('contain.text', 'Recibo REC-000001 baixado');
    });

    it('o recibo do pagamento estornado continua disponível para baixar', () => {
      stubMyReceipt(REVERSED_PAYMENT_ID, 'recibo-000004.pdf');
      cy.visit(MY_INVOICE_PATH);

      cy.get('button[aria-label="Baixar recibo REC-000004"]').click();

      cy.wait('@myReceipt');
      cy.get('[data-testid="notice"]').should('contain.text', 'Recibo REC-000004 baixado');
    });

    it('explica quando a API recusa o recibo', () => {
      stubMyReceiptRejected(PAID_PAYMENT_ID, 404, 'finance.payment.not_found');
      cy.visit(MY_INVOICE_PATH);

      cy.get('button[aria-label="Baixar recibo REC-000003"]').click();

      cy.wait('@myReceipt');
      cy.get('[data-testid="download-error"]').should(
        'contain.text',
        'Este recibo não está disponível',
      );
    });
  });

  it('navega para o mês seguinte, ainda aberto, e explica que a fatura aparece depois do fechamento', () => {
    cy.visit(MY_INVOICE_PATH);

    cy.get('[data-testid="next-month"]').click();

    cy.get('[data-testid="month-title"]').should('contain.text', 'Dezembro de 2026');
    cy.get('[data-testid="not-closed"]').should('contain.text', 'ainda não foi fechada');
    cy.get('[data-testid="unit-card"]').should('not.exist');
    cy.get('[data-testid="card-expenses"]').should('not.exist');
  });

  for (const theme of ['light', 'dark'] as const) {
    it(`mostra o aviso do mês aberto e o ícone em laranja, no tema ${theme}`, () => {
      cy.visit(`${MY_INVOICE_PATH}?mes=2026-12`, {
        onBeforeLoad: (win) => win.localStorage.setItem('urban.theme', theme),
      });

      cy.get('[data-testid="not-closed"]')
        .should('contain.text', 'ainda não foi fechada')
        .and('have.css', 'color', PALETTE[theme].warning);
      cy.get('[data-testid="not-closed"] mat-icon').should(
        'have.css',
        'color',
        PALETTE[theme].warning,
      );
    });
  }

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
