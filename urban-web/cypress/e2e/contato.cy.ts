import { PALETTE } from '../support/layout';
import { DESKTOP } from '../support/navegacao';
import { ADMIN } from '../support/pedidos';
import { stubSummary } from '../support/administracao';
import {
  CONTACT_PATH,
  MESSAGES_PATH,
  READ_MESSAGE,
  UNREAD_MESSAGE,
  stubContactMessages,
  stubContactSend,
  stubContactSendRejected,
} from '../support/contato';
import { MANAGER, stubAnonymous, stubSignedIn } from '../support/session';

const SUBJECT = 'Corrigir o CNPJ do condomínio';
const BODY = 'Digitei o CNPJ errado no cadastro. Pode corrigir para 11.222.333/0001-81?';

function writeMessage(subject = SUBJECT, body = BODY): void {
  cy.get('input[formControlName="subject"]').click('right').type(subject);
  cy.get('textarea[formControlName="body"]').click('right').type(body);
}

function messages(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="message"]');
}

describe('Contato do síndico', () => {
  beforeEach(() => {
    stubSignedIn(MANAGER);
    stubContactSend();
  });

  it('fica no canto inferior esquerdo do menu lateral e abre a tela', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit('/dashboard');

    cy.get('[data-testid="nav-contact"]').should('be.visible').and('contain.text', 'Contato');
    cy.get('[data-testid="nav-entries"]').then(($last) => {
      cy.get('[data-testid="nav-contact"]').then(($contact) => {
        const contact = $contact[0].getBoundingClientRect();
        expect(contact.top).to.be.greaterThan($last[0].getBoundingClientRect().bottom);
        expect(contact.left).to.be.lessThan(80);
        expect(contact.bottom).to.be.greaterThan(DESKTOP.height - 100);
      });
    });

    cy.get('[data-testid="nav-contact"]').click();

    cy.location('pathname').should('eq', CONTACT_PATH);
    cy.contains('h1', 'Contato').should('be.visible');
    cy.get('[data-testid="nav-contact"]').should('have.attr', 'aria-current', 'page');
  });

  it('envia a mensagem e confirma, com o texto aparado', () => {
    cy.visit(CONTACT_PATH);

    writeMessage(`  ${SUBJECT} `);
    cy.contains('button', 'Enviar mensagem').click();

    cy.wait('@send').its('request.body').should('deep.equal', { subject: SUBJECT, body: BODY });
    cy.get('[data-testid="sent"]')
      .should('be.visible')
      .and('contain.text', 'Mensagem enviada')
      .find('.text-success mat-icon')
      .should('have.css', 'color', PALETTE.light.success);
    cy.get('form').should('not.exist');
  });

  it('"Enviar outra mensagem" volta ao formulário vazio', () => {
    cy.visit(CONTACT_PATH);
    writeMessage();
    cy.contains('button', 'Enviar mensagem').click();

    cy.get('[data-testid="send-another"]').click();

    cy.get('input[formControlName="subject"]').should('have.value', '');
    cy.get('textarea[formControlName="body"]').should('have.value', '');
  });

  it('pede o que falta sem chamar a API', () => {
    cy.visit(CONTACT_PATH);

    cy.contains('button', 'Enviar mensagem').click();

    cy.get('mat-error').should('have.length', 2);
    cy.contains('mat-error', 'Informe o assunto').should('be.visible');
    cy.contains('mat-error', 'Informe a mensagem').should('be.visible');
    cy.get('@send.all').should('have.length', 0);
  });

  it('conta os caracteres da mensagem enquanto a pessoa escreve', () => {
    cy.visit(CONTACT_PATH);

    cy.get('textarea[formControlName="body"]').click('right').type('Olá, tudo bem?');

    cy.get('[data-testid="body-counter"]').should('contain.text', '14 de 2000 caracteres');
  });

  it('explica a recusa da API e guarda o que foi digitado', () => {
    stubContactSendRejected(403, 'identity.contact.send_forbidden');
    cy.visit(CONTACT_PATH);
    writeMessage();

    cy.contains('button', 'Enviar mensagem').click();

    cy.wait('@send');
    cy.get('[role="alert"]')
      .should('contain.text', 'Só o síndico pode enviar mensagens ao dono da plataforma')
      .find('mat-icon')
      .should('exist');
    cy.get('input[formControlName="subject"]').should('have.value', SUBJECT);
    cy.get('textarea[formControlName="body"]').should('have.value', BODY);
  });

  it('pede a entrada de quem não está logado e volta ao contato depois', () => {
    stubAnonymous();
    cy.visit(CONTACT_PATH);

    cy.location('pathname').should('eq', '/entrar');
    cy.location('search').should('contain', 'returnUrl=%2Fcontato');
  });

  it('leva o administrador de volta à visão geral', () => {
    stubSignedIn(ADMIN);
    stubSummary();
    cy.visit(CONTACT_PATH);

    cy.location('pathname').should('eq', '/admin/visao-geral');
  });
});

describe('Mensagens do administrador', () => {
  beforeEach(() => {
    stubSignedIn(ADMIN);
    stubSummary();
    stubContactMessages();
  });

  it('abre pelo menu lateral da Administração e lista as mensagens', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit('/dashboard');

    cy.get('[data-testid="nav-messages"]').click();

    cy.location('pathname').should('eq', MESSAGES_PATH);
    cy.contains('h1', 'Mensagens').should('be.visible');
    messages().should('have.length', 2);
    messages()
      .first()
      .should('contain.text', UNREAD_MESSAGE.subject)
      .and('contain.text', 'Ana')
      .and('contain.text', 'ana@example.com')
      .and('contain.text', 'Residencial Aurora')
      .and('contain.text', 'Enviada em 13/11/2026 às 12:05')
      .and('contain.text', 'Não lida');
    messages()
      .last()
      .should('contain.text', 'carlos@example.com')
      .and('contain.text', 'Ainda sem condomínio cadastrado')
      .and('contain.text', 'Lida em 12/11/2026 às 17:00');
  });

  it('não mostra o Contato do síndico no menu do administrador', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit(MESSAGES_PATH);

    cy.get('[data-testid="nav-messages"]').should('have.attr', 'aria-current', 'page');
    cy.get('[data-testid="nav-contact"]').should('not.exist');
  });

  it('marca como lida e avisa', () => {
    cy.visit(MESSAGES_PATH);
    messages().should('have.length', 2);

    messages().first().find('[data-testid="mark-read"]').click();

    cy.wait('@read').its('request.url').should('contain', `${UNREAD_MESSAGE.id}/read`);
    cy.get('[data-testid="notice"]')
      .should('contain.text', 'Mensagem marcada como lida')
      .find('.text-success mat-icon')
      .should('have.css', 'color', PALETTE.light.success);
    messages().first().should('contain.text', 'Lida em 13/11/2026 às 13:00');
    messages().first().find('[data-testid="mark-read"]').should('not.exist');
  });

  it('filtra só as não lidas e limpa o filtro', () => {
    cy.visit(MESSAGES_PATH);
    messages().should('have.length', 2);

    cy.get('[data-testid="filter-status"] button').click();
    cy.contains('[role="menuitemradio"]', 'Não lidas').click();

    messages().should('have.length', 1).and('contain.text', UNREAD_MESSAGE.subject);
    cy.get('[data-testid="total"]').should('contain.text', '1 mensagem');

    cy.get('[data-testid="clear-filters"]').click();
    messages().should('have.length', 2);
  });

  it('avisa quando a caixa de entrada está vazia', () => {
    stubContactMessages([]);
    cy.visit(MESSAGES_PATH);

    cy.get('[data-testid="empty"]').should('contain.text', 'Nenhuma mensagem recebida ainda');
  });

  it('responde por e-mail ao remetente', () => {
    cy.visit(MESSAGES_PATH);

    cy.get('[data-testid="reply"]')
      .first()
      .should('contain.text', 'Responder por e-mail')
      .and(
        'have.attr',
        'href',
        'mailto:ana@example.com?subject=Re%3A%20Corrigir%20o%20CNPJ%20do%20condom%C3%ADnio',
      );
  });

  it('leva o síndico de volta ao painel', () => {
    stubSignedIn(MANAGER);
    cy.visit(MESSAGES_PATH);

    cy.location('pathname').should('eq', '/dashboard');
  });

  it('mostra a mensagem lida sem o botão de marcar', () => {
    stubContactMessages([READ_MESSAGE]);
    cy.visit(MESSAGES_PATH);

    messages().should('have.length', 1);
    cy.get('[data-testid="mark-read"]').should('not.exist');
  });
});
