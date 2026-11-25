import { TODAY, stubFinanceRejected, stubMonth } from '../support/financeiro';
import { chooseMenu } from '../support/menu';
import {
  PAYMENTS_PATH,
  PAYMENT_ON_102,
  StubPayment,
  stubChargePayments,
  stubPaymentRegistration,
  stubPayments,
  stubPaymentsNotClosed,
  stubReceipt,
  stubReceiptRejected,
} from '../support/pagamentos';
import { expectErrorsToStayInTheirOwnSpace } from '../support/layout';
import { DESKTOP } from '../support/navegacao';
import { stubSignedIn } from '../support/session';

/** O rótulo do Material cobre o meio do campo vazio, então o clique vai para a direita (como em "Meus dados"). */
function enter(field: Cypress.Chainable<JQuery<HTMLElement>>, value: string): void {
  field.click('right').type(`{selectall}${value}`);
}

/** O Material leva o foco ao primeiro campo só depois de abrir o diálogo; digitar antes disso perde o foco. */
function dialogHasFocus(tag: string): void {
  cy.focused().closest(tag).should('exist');
}

function rows(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="charge-row"]');
}

describe('Pagamentos do mês', () => {
  beforeEach(() => {
    cy.clock(TODAY, ['Date']);
    stubSignedIn();
  });

  it('abre pelo menu lateral, no mês de hoje, com o resumo e uma linha por unidade', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    stubPayments();
    stubMonth();
    cy.visit('/dashboard');

    cy.get('[data-testid="nav-payments"]').click();

    cy.location('pathname').should('eq', PAYMENTS_PATH);
    cy.contains('h1', 'Pagamentos').should('be.visible');
    cy.get('[data-testid="month-title"]').should('contain.text', 'Novembro de 2026');
    cy.get('[data-testid="stat-charged"]')
      .invoke('text')
      .should('match', /R\$\s2\.400,00/);
    cy.get('[data-testid="stat-received"]')
      .invoke('text')
      .should('match', /R\$\s1\.300,00/);
    cy.get('[data-testid="stat-to-receive"]')
      .invoke('text')
      .should('match', /R\$\s1\.100,00/);
    cy.get('[data-testid="due-date"]').should('contain.text', 'Vencimento em 10/12/2026');
    rows().should('have.length', 3);
    rows().first().should('contain.text', 'Bloco A · 101').and('contain.text', 'Atrasada');
    rows().eq(1).should('contain.text', 'Parcial');
    rows().eq(2).should('contain.text', 'Paga');
  });

  it('filtra pela situação e limpa o filtro', () => {
    stubPayments();
    cy.visit(PAYMENTS_PATH);
    rows().should('have.length', 3);

    chooseMenu('[data-testid="filter-status"]', 'Atrasadas');

    rows().should('have.length', 1).first().should('contain.text', 'Bloco A · 101');
    cy.get('[data-testid="clear-filter"]').click();
    rows().should('have.length', 3);
  });

  it('explica que o mês não foi fechado e leva aos lançamentos do mês', () => {
    stubPaymentsNotClosed();
    stubMonth();
    cy.visit(PAYMENTS_PATH);

    cy.get('[data-testid="not-closed"]').should(
      'contain.text',
      'Novembro de 2026 ainda não foi fechado',
    );
    cy.get('[data-testid="close-link"]').click();

    cy.location('pathname').should('eq', '/financeiro/lancamentos');
    cy.location('search').should('eq', '?mes=2026-11');
  });

  describe('registro', () => {
    it('registra um pagamento parcial e mostra o novo saldo', () => {
      const charges = stubPayments();
      stubPaymentRegistration(charges);
      cy.visit(PAYMENTS_PATH);

      cy.get('button[aria-label="Registrar pagamento de Bloco A · 102"]').click();
      cy.get('urban-register-payment-dialog h2').should(
        'contain.text',
        'Registrar pagamento de Bloco A · 102',
      );
      dialogHasFocus('urban-register-payment-dialog');
      cy.get('urban-register-payment-dialog [formControlName="amount"]').should(
        'have.value',
        '300,00',
      );
      cy.get('urban-register-payment-dialog [formControlName="receivedOn"]').should(
        'have.value',
        '15/11/2026',
      );
      enter(cy.get('urban-register-payment-dialog [formControlName="amount"]'), '100');
      enter(cy.get('urban-register-payment-dialog [formControlName="note"]'), 'No caixa');
      cy.get('urban-register-payment-dialog').contains('button', 'Registrar pagamento').click();

      cy.wait('@register').its('request.body').should('deep.equal', {
        amount: 100,
        receivedOn: '2026-11-15',
        method: 'pix',
        note: 'No caixa',
      });
      cy.get('urban-register-payment-dialog').should('not.exist');
      cy.get('[data-testid="notice"]').should('contain.text', 'registrado em Bloco A · 102');
      rows()
        .eq(1)
        .find('[data-testid="balance"]')
        .invoke('text')
        .should('match', /R\$\s200,00/);
      cy.get('[data-testid="stat-received"]')
        .invoke('text')
        .should('match', /R\$\s1\.400,00/);
    });

    it('quita a cobrança e tira o botão de registrar', () => {
      const charges = stubPayments();
      stubPaymentRegistration(charges);
      cy.visit(PAYMENTS_PATH);

      cy.get('button[aria-label="Registrar pagamento de Bloco A · 102"]').click();
      dialogHasFocus('urban-register-payment-dialog');
      cy.get('urban-register-payment-dialog').contains('button', 'Registrar pagamento').click();

      cy.wait('@register');
      rows().eq(1).should('contain.text', 'Paga');
      cy.get('button[aria-label="Registrar pagamento de Bloco A · 102"]').should('not.exist');
    });

    it('avisa no próprio diálogo quando o valor passa do saldo', () => {
      stubPayments();
      stubFinanceRejected(
        'POST',
        /\/api\/finance\/charges\/[^/]+\/payments$/,
        422,
        'finance.payment.exceeds_balance',
        'register',
        { balance: 120 },
      );
      cy.visit(PAYMENTS_PATH);

      cy.get('button[aria-label="Registrar pagamento de Bloco A · 102"]').click();
      dialogHasFocus('urban-register-payment-dialog');
      cy.get('urban-register-payment-dialog').contains('button', 'Registrar pagamento').click();

      cy.get('urban-register-payment-dialog [role="alert"]')
        .invoke('text')
        .should('match', /O valor passa do saldo atual de R\$\s120,00/);
    });

    it('não deixa registrar valor acima do saldo nem data futura, e os avisos não cobrem os campos', () => {
      stubPayments();
      cy.visit(PAYMENTS_PATH);

      cy.get('button[aria-label="Registrar pagamento de Bloco A · 102"]').click();
      dialogHasFocus('urban-register-payment-dialog');
      enter(cy.get('urban-register-payment-dialog [formControlName="amount"]'), '300,01');
      enter(cy.get('urban-register-payment-dialog [formControlName="receivedOn"]'), '16/11/2026');
      cy.get('urban-register-payment-dialog').contains('button', 'Registrar pagamento').click();

      cy.get('urban-register-payment-dialog mat-error').should('have.length', 2);
      expectErrorsToStayInTheirOwnSpace('urban-register-payment-dialog');
    });
  });

  describe('histórico e estorno', () => {
    const PAYMENTS: StubPayment[] = [{ ...PAYMENT_ON_102 }];

    it('mostra os pagamentos da cobrança e estorna com motivo', () => {
      const charges = stubPayments();
      stubChargePayments(charges[1], PAYMENTS);
      cy.visit(PAYMENTS_PATH);

      cy.get('button[aria-label="Ver pagamentos de Bloco A · 102"]').click();
      cy.get('urban-charge-payments-dialog h2').should(
        'contain.text',
        'Pagamentos de Bloco A · 102',
      );
      cy.get('[data-testid="payment-info"]').should('contain.text', '10/11/2026 · Pix');
      cy.get('[data-testid="reverse-payment"]').click();
      cy.get('urban-charge-payments-dialog [formControlName="reason"]').should('be.visible');
      enter(
        cy.get('urban-charge-payments-dialog [formControlName="reason"]'),
        'Lançado na unidade errada',
      );
      cy.get('[data-testid="confirm-reverse"]').click();

      cy.wait('@reverse').its('request.body').should('deep.equal', {
        reason: 'Lançado na unidade errada',
      });
      cy.get('[data-testid="reverse-notice"]').should('contain.text', 'Pagamento estornado');
      cy.get('[data-testid="reversed"]').should('contain.text', 'Estornado em');
      cy.get('[data-testid="reverse-payment"]').should('not.exist');
      cy.get('[data-testid="close"]').click();

      cy.get('urban-charge-payments-dialog').should('not.exist');
      cy.get('[data-testid="notice"]').should('contain.text', 'Estorno registrado');
    });

    it('exige o motivo do estorno', () => {
      const charges = stubPayments();
      stubChargePayments(charges[1], [{ ...PAYMENT_ON_102 }]);
      cy.visit(PAYMENTS_PATH);

      cy.get('button[aria-label="Ver pagamentos de Bloco A · 102"]').click();
      cy.get('[data-testid="reverse-payment"]').click();
      cy.get('[data-testid="confirm-reverse"]').click();

      cy.get('urban-charge-payments-dialog mat-error').should(
        'contain.text',
        'Informe o motivo do estorno',
      );
      expectErrorsToStayInTheirOwnSpace('urban-charge-payments-dialog');
    });
  });

  describe('recibo', () => {
    it('mostra o código do recibo de cada pagamento e baixa o PDF pela rota do síndico', () => {
      const charges = stubPayments();
      stubChargePayments(charges[1], [{ ...PAYMENT_ON_102 }]);
      stubReceipt(PAYMENT_ON_102.id, 'recibo-000001.pdf');
      cy.visit(PAYMENTS_PATH);

      cy.get('button[aria-label="Ver pagamentos de Bloco A · 102"]').click();
      cy.get('[data-testid="receipt-code"]').should('contain.text', 'Recibo REC-000001');
      cy.get('button[aria-label="Baixar recibo REC-000001"]').click();

      cy.wait('@receipt')
        .its('request.url')
        .should('include', `/api/finance/payments/${PAYMENT_ON_102.id}/receipt`);
      cy.get('[data-testid="receipt-notice"]').should('contain.text', 'Recibo REC-000001 baixado');
    });

    it('o pagamento estornado mostra o recibo cancelado e ainda deixa baixá-lo', () => {
      const charges = stubPayments();
      stubChargePayments(charges[1], [
        {
          ...PAYMENT_ON_102,
          reversedAt: '2026-11-12T14:00:00Z',
          reversalReason: 'Lançado na unidade errada',
        },
      ]);
      stubReceipt(PAYMENT_ON_102.id, 'recibo-000001.pdf');
      cy.visit(PAYMENTS_PATH);

      cy.get('button[aria-label="Ver pagamentos de Bloco A · 102"]').click();

      cy.get('[data-testid="receipt-code"]').should(
        'contain.text',
        'Recibo REC-000001 · cancelado',
      );
      cy.get('[data-testid="download-receipt"]').click();
      cy.wait('@receipt');
      cy.get('[data-testid="receipt-notice"]').should('exist');
    });

    it('explica quando a API recusa o recibo, no próprio diálogo', () => {
      const charges = stubPayments();
      stubChargePayments(charges[1], [{ ...PAYMENT_ON_102 }]);
      stubReceiptRejected(PAYMENT_ON_102.id, 404, 'finance.payment.not_found');
      cy.visit(PAYMENTS_PATH);

      cy.get('button[aria-label="Ver pagamentos de Bloco A · 102"]').click();
      cy.get('[data-testid="download-receipt"]').click();

      cy.wait('@receipt');
      cy.get('[data-testid="receipt-error"]').should(
        'contain.text',
        'Este pagamento não existe mais',
      );
    });
  });
});
