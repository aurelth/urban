import { expectErrorsToStayInTheirOwnSpace } from '../support/layout';
import { stubResetPassword } from '../support/senha';
import { stubAnonymous, stubSignedIn } from '../support/session';

const PASSWORD = 'Nova-senha-123';

describe('Redefinir senha', () => {
  beforeEach(() => {
    stubAnonymous();
  });

  it('troca a senha pelo link do e-mail e leva a entrar', () => {
    stubResetPassword();
    cy.visit('/redefinir-senha?token=token-do-link');

    cy.contains('h1', 'Redefinir senha').should('be.visible');
    cy.get('input[autocomplete="new-password"]').type(PASSWORD);
    cy.contains('button', 'Salvar senha').click();

    cy.wait('@reset')
      .its('request.body')
      .should('deep.equal', { token: 'token-do-link', password: PASSWORD });
    cy.contains('h1', 'Senha alterada').should('be.visible');
    cy.contains('a', 'Entrar').click();
    cy.location('pathname').should('eq', '/entrar');
  });

  it('não envia uma senha fora da política', () => {
    stubResetPassword();
    cy.visit('/redefinir-senha?token=token-do-link');

    cy.get('input[autocomplete="new-password"]').type('curta1');
    cy.contains('button', 'Salvar senha').click();

    cy.contains('A senha precisa ter pelo menos 10 caracteres').should('be.visible');
    cy.get('@reset.all').should('have.length', 0);
  });

  it('mantém o aviso da senha longe do botão em uma janela estreita', () => {
    cy.viewport(320, 640);
    stubResetPassword();
    cy.visit('/redefinir-senha?token=token-do-link');

    cy.get('input[autocomplete="new-password"]').type('curta1');
    cy.contains('button', 'Salvar senha').click();

    cy.contains('mat-error', 'A senha precisa ter pelo menos 10 caracteres').should('be.visible');
    expectErrorsToStayInTheirOwnSpace();
  });

  it('manda pedir um novo link quando o link venceu', () => {
    stubResetPassword(400, 'identity.password_reset.expired');
    cy.visit('/redefinir-senha?token=token-vencido');

    cy.get('input[autocomplete="new-password"]').type(PASSWORD);
    cy.contains('button', 'Salvar senha').click();

    cy.contains('h1', 'Link inválido').should('be.visible');
    cy.get('[role="alert"]').should('contain.text', 'Link expirado');
    cy.contains('a', 'Pedir novo link').click();
    cy.location('pathname').should('eq', '/esqueci-a-senha');
  });

  it('avisa quando o link veio sem o token', () => {
    cy.visit('/redefinir-senha');

    cy.contains('h1', 'Link incompleto').should('be.visible');
    cy.contains('a', 'Pedir novo link').should('be.visible');
  });

  it('abre mesmo com uma sessão ativa neste navegador', () => {
    stubSignedIn();
    cy.visit('/redefinir-senha?token=token-do-link');

    cy.location('pathname').should('eq', '/redefinir-senha');
    cy.contains('h1', 'Redefinir senha').should('be.visible');
  });
});
