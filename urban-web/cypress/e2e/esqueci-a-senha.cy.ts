import { stubForgotPassword } from '../support/senha';
import { stubAnonymous, stubSignedIn } from '../support/session';

const EMAIL = 'sindica@example.com';

describe('Esqueci a senha', () => {
  beforeEach(() => {
    stubAnonymous();
  });

  it('chega pelo link da tela de entrada e volta por ele', () => {
    cy.visit('/entrar');

    cy.contains('a', 'Esqueci a senha').click();

    cy.location('pathname').should('eq', '/esqueci-a-senha');
    cy.contains('h1', 'Esqueci a senha').should('be.visible');

    cy.contains('a', 'Entrar').click();

    cy.location('pathname').should('eq', '/entrar');
  });

  it('leva quem já está logado de volta ao painel', () => {
    stubSignedIn();
    cy.visit('/esqueci-a-senha');

    cy.location('pathname').should('eq', '/dashboard');
  });

  it('pede o link com o e-mail e avisa para verificar a caixa de entrada', () => {
    stubForgotPassword();
    cy.visit('/esqueci-a-senha');

    cy.get('input[type="email"]').type(EMAIL);
    cy.contains('button', 'Enviar link').click();

    cy.wait('@forgot').its('request.body').should('deep.equal', { email: EMAIL });
    cy.contains('h1', 'Verifique seu e-mail').should('be.visible');
    cy.get('[role="status"]').should('contain.text', EMAIL).and('contain.text', '1 hora');
  });

  it('não envia com um e-mail inválido', () => {
    stubForgotPassword();
    cy.visit('/esqueci-a-senha');

    cy.get('input[type="email"]').type('isto-nao-e-email');
    cy.contains('button', 'Enviar link').click();

    cy.contains('Informe um e-mail válido').should('be.visible');
    cy.get('@forgot.all').should('have.length', 0);
  });

  it('pede para aguardar quando o limite de pedidos estoura', () => {
    stubForgotPassword(429, undefined, '30');
    cy.visit('/esqueci-a-senha');

    cy.get('input[type="email"]').type(EMAIL);
    cy.contains('button', 'Enviar link').click();

    cy.get('[role="alert"]').should('contain.text', 'Aguarde 30 segundos');
  });
});
