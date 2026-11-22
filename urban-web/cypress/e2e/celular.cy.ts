import { MANAGERS_PATH, OVERVIEW_PATH, stubManagers } from '../support/administracao';
import {
  CONTACT_PATH,
  MESSAGES_PATH,
  stubContactMessages,
  stubContactSend,
} from '../support/contato';
import { stubConfirmEmailCheck, stubTerms } from '../support/cadastro';
import { NEW_MANAGER } from '../support/condominio';
import { MANAGER, RESIDENT, stubAnonymous, stubSignedIn } from '../support/session';
import { READINGS_PATH, stubReadings } from '../support/leituras';
import { INSTRUCTIONS_PATH, NO_INSTRUCTIONS, stubInstructions } from '../support/dados-pagamento';
import { INVOICE_DELIVERIES_PATH, stubInvoiceDeliveries } from '../support/enviar-faturas';
import { DELINQUENCY_PATH, stubDelinquency } from '../support/inadimplencia';
import { PAYMENTS_PATH, stubPayments } from '../support/pagamentos';
import { MY_INVOICE_PATH, stubMyMonth } from '../support/minha-fatura';
import { MONTH_DASHBOARD_PATH, stubMonthDashboard } from '../support/painel-financeiro';
import { stubDashboard } from '../support/painel';
import { ADMIN, REQUESTS_PATH, stubRequests } from '../support/pedidos';
import { TARIFFS_PATH, stubTariffs } from '../support/tarifas';

/** Com `should` o Cypress repete a conferência até a página assentar (a barra de rolagem aparece depois do título). */
function expectNoHorizontalScroll(): void {
  cy.document().should((doc) => {
    expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
  });
}

const PUBLIC_SCREENS = [
  ['/entrar', 'Entrar'],
  ['/cadastrar', 'Criar conta'],
  ['/esqueci-a-senha', 'Esqueci a senha'],
  ['/termos', 'Termos de uso'],
  ['/privacidade', 'Política de privacidade'],
];

describe('Tela de celular (375 x 667)', () => {
  beforeEach(() => {
    cy.viewport(375, 667);
  });

  PUBLIC_SCREENS.forEach(([path, title]) => {
    it(`mostra ${path} inteira, sem rolagem horizontal`, () => {
      stubAnonymous();
      cy.visit(path);

      cy.contains('h1', title).should('be.visible');
      expectNoHorizontalScroll();
    });
  });

  it('mostra a criação de senha inteira, sem rolagem horizontal', () => {
    stubAnonymous();
    stubTerms();
    stubConfirmEmailCheck();
    cy.visit('/confirmar-email?token=token-do-link');

    cy.contains('h1', 'Criar senha').should('be.visible');
    cy.get('input[autocomplete="new-password"]').should('be.visible');
    cy.get('mat-checkbox').should('be.visible');
    cy.contains('button', 'Ativar conta').should('be.visible');
    expectNoHorizontalScroll();
  });

  it('mostra a nova senha inteira, sem rolagem horizontal', () => {
    stubAnonymous();
    cy.visit('/redefinir-senha?token=token-do-link');

    cy.contains('h1', 'Redefinir senha').should('be.visible');
    cy.get('input[autocomplete="new-password"]').should('be.visible');
    cy.contains('button', 'Salvar senha').should('be.visible');
    expectNoHorizontalScroll();
  });

  it('mostra o painel em coluna única, com a saída à mão e sem rolagem horizontal', () => {
    stubSignedIn(MANAGER);
    stubDashboard({ month: 'closed', overdueCharges: 3, instructions: NO_INSTRUCTIONS });
    cy.visit('/dashboard');

    cy.get('[data-testid="user-menu"]').should('be.visible');
    cy.get('[data-testid="stat-units"]').should('be.visible');
    cy.get('[data-testid="pending"]').should('have.length.at.least', 1);
    const cards = ['card-month', 'card-payments', 'card-condominium', 'card-pendings'];
    cy.get(cards.map((card) => `[data-testid="${card}"]`).join(',')).then(($cards) => {
      expect($cards).to.have.length(4);
      const lefts = [...$cards].map((card) => Math.round(card.getBoundingClientRect().left));
      expect(new Set(lefts).size).to.eq(1);
    });
    expectNoHorizontalScroll();
  });

  it('mostra os pedidos de cadastro em coluna única, sem rolagem horizontal', () => {
    stubSignedIn(ADMIN);
    stubRequests();
    cy.visit(REQUESTS_PATH);

    cy.contains('h1', 'Pedidos de cadastro').should('be.visible');
    cy.get('[data-testid="request"]').should('have.length', 2);
    cy.get('[data-testid="request"]').each(($request) => {
      const { left, right } = $request[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(375);
    });
    cy.get('[data-testid="approve"]').first().should('be.visible');
    expectNoHorizontalScroll();
  });

  it('mostra o contato do síndico em coluna única, sem rolagem horizontal', () => {
    stubSignedIn(MANAGER);
    stubContactSend();
    cy.visit(CONTACT_PATH);

    cy.contains('h1', 'Contato').should('be.visible');
    cy.get('input[formControlName="subject"]').should('be.visible');
    cy.get('textarea[formControlName="body"]').then(($field) => {
      const { left, right } = $field[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(375);
    });
    cy.contains('button', 'Enviar mensagem').should('be.visible');
    expectNoHorizontalScroll();
  });

  it('mostra as mensagens do administrador em coluna única, sem rolagem horizontal', () => {
    stubSignedIn(ADMIN);
    stubContactMessages();
    cy.visit(MESSAGES_PATH);

    cy.contains('h1', 'Mensagens').should('be.visible');
    cy.get('[data-testid="message"]').should('have.length', 2);
    cy.get('[data-testid="message"]').each(($message) => {
      const { left, right } = $message[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(375);
    });
    cy.get('[data-testid="mark-read"]').first().should('be.visible');
    expectNoHorizontalScroll();
  });

  it('mostra a resposta ao síndico, com anexo de nome comprido, sem rolagem horizontal', () => {
    stubSignedIn(ADMIN);
    stubContactMessages();
    cy.visit(MESSAGES_PATH);

    cy.get('[data-testid="reply"]').first().click();
    cy.get('[data-testid="file-input"]').selectFile(
      {
        contents: Cypress.Buffer.from('texto'),
        fileName:
          'relatorio-de-prestacao-de-contas-do-condominio-residencial-aurora-novembro-2026.txt',
      },
      { force: true },
    );

    cy.get('[data-testid="reply-form"]').then(($form) => {
      const { left, right } = $form[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(375);
    });
    cy.get('[data-testid="attachment-name"]').should('be.visible');
    cy.contains('button', 'Enviar resposta').should('be.visible');
    cy.get('[data-testid="cancel-reply"]').should('be.visible');
    expectNoHorizontalScroll();
  });

  it('mostra a visão geral do administrador em coluna única, sem rolagem horizontal', () => {
    stubSignedIn(ADMIN);
    stubManagers();
    cy.visit(OVERVIEW_PATH);

    cy.contains('h1', 'Visão geral').should('be.visible');
    cy.get('[data-testid="stat-active"]').should('be.visible');
    cy.get('[data-testid^="stat-"]').each(($stat) => {
      const { left, right } = $stat[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(375);
    });
    expectNoHorizontalScroll();
  });

  it('mostra os síndicos em coluna única, com as ações à mão e sem rolagem horizontal', () => {
    stubSignedIn(ADMIN);
    stubManagers();
    cy.visit(MANAGERS_PATH);

    cy.contains('h1', 'Síndicos').should('be.visible');
    cy.get('[data-testid="manager-row"]').should('have.length', 2);
    cy.get('[data-testid="manager-row"]').each(($row) => {
      const { left, right } = $row[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(375);
    });
    cy.get('button[aria-label="Suspender Ana"]').should('be.visible');
    expectNoHorizontalScroll();
  });

  it('mostra as tarifas de consumo em coluna única, sem rolagem horizontal', () => {
    stubSignedIn(MANAGER);
    stubTariffs();
    cy.visit(TARIFFS_PATH);

    cy.contains('h1', 'Tarifas de consumo').should('be.visible');
    cy.get('[data-testid="tariff-item"]').should('have.length', 2);
    cy.get('[data-testid="new-tariff"]').should('be.visible');
    expectNoHorizontalScroll();
  });

  it('mostra os dados para pagamento em coluna única, sem rolagem horizontal', () => {
    stubSignedIn(MANAGER);
    stubInstructions();
    cy.visit(INSTRUCTIONS_PATH);

    cy.contains('h1', 'Dados para pagamento').should('be.visible');
    cy.get('[data-testid="pix-section"]').should('be.visible');
    cy.get('[data-testid="account-section"]').should('be.visible');
    cy.get('input[formControlName="pixKey"]').then(($field) => {
      const { left, right } = $field[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(375);
    });
    expectNoHorizontalScroll();
  });

  it('mostra as leituras do mês em coluna única, sem rolagem horizontal', () => {
    stubSignedIn(MANAGER);
    stubReadings();
    cy.visit(READINGS_PATH);

    cy.contains('h1', 'Leituras do mês').should('be.visible');
    cy.get('[data-testid="unit-row"]').should('have.length', 3);
    cy.get('[data-testid="unit-row"] mat-form-field').each(($field) => {
      const { left, right } = $field[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(375);
    });
    expectNoHorizontalScroll();
  });

  it('mostra os pagamentos do mês sem rolagem horizontal, com os botões dentro da tela', () => {
    stubSignedIn(MANAGER);
    stubPayments();
    cy.visit(PAYMENTS_PATH);

    cy.contains('h1', 'Pagamentos').should('be.visible');
    cy.get('[data-testid="charge-row"]').should('have.length', 3);
    cy.get('[data-testid="charge-row"] button').each(($button) => {
      const { left, right } = $button[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(375);
    });
    expectNoHorizontalScroll();
  });

  it('mostra o painel financeiro sem rolagem horizontal, com as setas de mês dentro da tela', () => {
    stubSignedIn(MANAGER);
    stubMonthDashboard();
    cy.visit(MONTH_DASHBOARD_PATH);

    cy.contains('h1', 'Painel financeiro').should('be.visible');
    cy.get('[data-testid="card-charges"]').should('be.visible');
    cy.get('[data-testid="previous-month"], [data-testid="next-month"]').each(($button) => {
      const { left, right } = $button[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(375);
    });
    expectNoHorizontalScroll();
  });

  it('mostra a fatura do morador sem rolagem horizontal, com botões e setas dentro da tela', () => {
    stubSignedIn(RESIDENT);
    stubMyMonth();
    cy.visit(MY_INVOICE_PATH);

    cy.contains('h1', 'Minha fatura').should('be.visible');
    cy.get('[data-testid="unit-card"]').should('have.length', 2);
    cy.get(
      '[data-testid="previous-month"], [data-testid="next-month"], [data-testid="download-invoice"]',
    ).each(($button) => {
      const { left, right } = $button[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(375);
    });
    expectNoHorizontalScroll();
  });

  it('mostra a inadimplência sem rolagem horizontal, com os botões dentro da tela', () => {
    stubSignedIn(MANAGER);
    stubDelinquency();
    cy.visit(DELINQUENCY_PATH);

    cy.contains('h1', 'Inadimplência').should('be.visible');
    cy.get('[data-testid="debtor"]').should('have.length', 3);
    cy.get('[data-testid="debtor"] button').should('have.length', 4);
    cy.get('[data-testid="debtor"] button').each(($button) => {
      const { left, right } = $button[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(375);
    });
    expectNoHorizontalScroll();
  });

  it('mostra o envio de faturas do mês sem rolagem horizontal, com os botões dentro da tela', () => {
    stubSignedIn(MANAGER);
    stubInvoiceDeliveries();
    cy.visit(INVOICE_DELIVERIES_PATH);

    cy.contains('h1', 'Enviar faturas').should('be.visible');
    cy.get('[data-testid="delivery-row"]').should('have.length', 4);
    cy.get('[data-testid="send-month"]').click();
    cy.get('[data-testid="send-confirm"]').should('be.visible');
    cy.get('[data-testid="delivery-row"] button, [data-testid="send-confirm"] button').each(
      ($button) => {
        const { left, right } = $button[0].getBoundingClientRect();
        expect(left).to.be.at.least(0);
        expect(right).to.be.at.most(375);
      },
    );
    expectNoHorizontalScroll();
  });

  it('mostra o cadastro do condomínio sem rolagem horizontal e com todos os campos na tela', () => {
    stubSignedIn(NEW_MANAGER);
    cy.visit('/condominio/cadastrar');

    cy.contains('h1', 'Cadastre seu condomínio').should('be.visible');
    cy.contains('button', 'Cadastrar condomínio').should('exist');
    cy.get('mat-form-field').should('have.length.greaterThan', 15);
    cy.get('mat-form-field').each(($field) => {
      const { left, right } = $field[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(375);
    });
    expectNoHorizontalScroll();
  });
});
