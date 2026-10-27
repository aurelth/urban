import {
  MANAGER,
  stubAnonymous,
  stubLogin,
  stubLoginRejected,
  stubSignedIn,
} from '../support/session';

describe('Entrar', () => {
  it('manda quem não está logado para a entrada, guardando o destino', () => {
    stubAnonymous();
    cy.visit('/dashboard');

    cy.location('pathname').should('eq', '/entrar');
    cy.location('search').should('contain', 'returnUrl=%2Fdashboard');
    cy.contains('h1', 'Entrar').should('be.visible');
  });

  it('entra com e-mail e senha e abre o painel', () => {
    stubAnonymous();
    stubLogin();
    cy.visit('/entrar');

    cy.get('input[type="email"]').type(MANAGER.email);
    cy.get('input[autocomplete="current-password"]').type('Senha-segura-123');
    cy.contains('button', 'Entrar').click();

    cy.wait('@login').its('request.body').should('deep.equal', {
      email: MANAGER.email,
      password: 'Senha-segura-123',
    });
    cy.location('pathname').should('eq', '/dashboard');
    cy.get('[data-testid="current-user"]').should('contain.text', MANAGER.email);
  });

  it('mostra o motivo quando a senha está errada', () => {
    stubAnonymous();
    stubLoginRejected(401, 'identity.login.invalid_credentials');
    cy.visit('/entrar');

    cy.get('input[type="email"]').type(MANAGER.email);
    cy.get('input[autocomplete="current-password"]').type('senha-errada-1');
    cy.contains('button', 'Entrar').click();

    cy.get('[role="alert"]').should('contain.text', 'E-mail ou senha incorretos');
    cy.location('pathname').should('eq', '/entrar');
  });

  it('pede os campos obrigatórios sem chamar a API', () => {
    stubAnonymous();
    cy.visit('/entrar');

    cy.contains('button', 'Entrar').click();

    cy.contains('Informe o e-mail').should('be.visible');
    cy.contains('Informe a senha').should('be.visible');
    cy.focused().should('have.attr', 'type', 'email');
  });

  it('mantém a sessão ao recarregar a página', () => {
    stubSignedIn();
    cy.visit('/dashboard');
    cy.get('[data-testid="current-user"]').should('contain.text', MANAGER.email);

    cy.reload();

    cy.location('pathname').should('eq', '/dashboard');
    cy.get('[data-testid="current-user"]').should('contain.text', MANAGER.email);
  });

  it('leva quem já está logado de volta ao painel', () => {
    stubSignedIn();
    cy.visit('/entrar');

    cy.location('pathname').should('eq', '/dashboard');
  });

  it('estica os campos até a largura do formulário', () => {
    stubAnonymous();
    cy.visit('/entrar');

    cy.get('form').then(($form) => {
      const width = $form[0].getBoundingClientRect().width;
      cy.get('mat-form-field').each(($field) => {
        expect($field[0].getBoundingClientRect().width).to.be.closeTo(width, 1);
      });
    });
  });

  it('sai da conta e volta para a entrada', () => {
    stubSignedIn();
    cy.visit('/dashboard');

    cy.get('[data-testid="sign-out"]').click();

    cy.location('pathname').should('eq', '/entrar');
    cy.contains('h1', 'Entrar').should('be.visible');
  });
});
