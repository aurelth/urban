import { TERMS_VERSION, stubConfirmEmail, stubRegister, stubTerms } from '../support/cadastro';
import { stubAnonymous, stubSignedIn } from '../support/session';

const EMAIL = 'sindica@example.com';
const PASSWORD = 'Senha-segura-123';

describe('Cadastro', () => {
  beforeEach(() => {
    stubAnonymous();
  });

  it('chega pelo link da tela de entrada', () => {
    cy.visit('/entrar');

    cy.contains('a', 'Criar conta').click();

    cy.location('pathname').should('eq', '/cadastrar');
    cy.contains('h1', 'Criar conta').should('be.visible');
  });

  it('leva quem já está logado de volta ao painel', () => {
    stubSignedIn();
    cy.visit('/cadastrar');

    cy.location('pathname').should('eq', '/dashboard');
  });

  it('pede o link com o e-mail e avisa para verificar a caixa de entrada', () => {
    stubRegister();
    cy.visit('/cadastrar');

    cy.get('input[type="email"]').type(EMAIL);
    cy.contains('button', 'Enviar link').click();

    cy.wait('@register').its('request.body').should('deep.equal', { email: EMAIL });
    cy.contains('h1', 'Verifique seu e-mail').should('be.visible');
    cy.get('[role="status"]').should('contain.text', EMAIL);
  });

  it('não envia com um e-mail inválido', () => {
    stubRegister();
    cy.visit('/cadastrar');

    cy.get('input[type="email"]').type('isto-nao-e-email');
    cy.contains('button', 'Enviar link').click();

    cy.contains('Informe um e-mail válido').should('be.visible');
    cy.get('@register.all').should('have.length', 0);
  });

  it('pede para aguardar quando o limite de pedidos estoura', () => {
    stubRegister(429, undefined, '30');
    cy.visit('/cadastrar');

    cy.get('input[type="email"]').type(EMAIL);
    cy.contains('button', 'Enviar link').click();

    cy.get('[role="alert"]').should('contain.text', 'Aguarde 30 segundos');
  });
});

describe('Confirmação do e-mail', () => {
  beforeEach(() => {
    stubAnonymous();
  });

  it('cria a senha, aceita os termos e ativa a conta', () => {
    stubTerms();
    stubConfirmEmail();
    cy.visit('/confirmar-email?token=token-do-link');
    cy.wait('@terms');

    cy.get('input[autocomplete="new-password"]').type(PASSWORD);
    cy.get('mat-checkbox input').check({ force: true });
    cy.contains('button', 'Ativar conta').click();

    cy.wait('@confirm').its('request.body').should('deep.equal', {
      token: 'token-do-link',
      password: PASSWORD,
      termsVersion: TERMS_VERSION,
    });
    cy.contains('h1', 'Conta ativada').should('be.visible');
    cy.contains('a', 'Entrar').click();
    cy.location('pathname').should('eq', '/entrar');
  });

  it('não envia sem aceitar os termos', () => {
    stubTerms();
    stubConfirmEmail();
    cy.visit('/confirmar-email?token=token-do-link');
    cy.wait('@terms');

    cy.get('input[autocomplete="new-password"]').type(PASSWORD);
    cy.contains('button', 'Ativar conta').click();

    cy.contains('Aceite os termos para continuar').should('be.visible');
    cy.get('@confirm.all').should('have.length', 0);
  });

  it('manda pedir um novo cadastro quando o link venceu', () => {
    stubTerms();
    stubConfirmEmail(400, 'identity.email_verification.expired');
    cy.visit('/confirmar-email?token=token-vencido');
    cy.wait('@terms');

    cy.get('input[autocomplete="new-password"]').type(PASSWORD);
    cy.get('mat-checkbox input').check({ force: true });
    cy.contains('button', 'Ativar conta').click();

    cy.contains('h1', 'Link inválido').should('be.visible');
    cy.get('[role="alert"]').should('contain.text', 'Link expirado');
    cy.contains('a', 'Fazer novo cadastro').click();
    cy.location('pathname').should('eq', '/cadastrar');
  });

  it('avisa quando o link veio sem o token', () => {
    cy.visit('/confirmar-email');

    cy.contains('h1', 'Link incompleto').should('be.visible');
    cy.contains('a', 'Fazer novo cadastro').should('be.visible');
  });
});

describe('Termos de uso e política de privacidade', () => {
  beforeEach(() => {
    stubAnonymous();
  });

  it('mostra os termos como rascunho, com a versão', () => {
    cy.visit('/termos');

    cy.contains('h1', 'Termos de uso').should('be.visible');
    cy.get('[role="note"]').should('contain.text', 'Rascunho');
    cy.contains(`Versão ${TERMS_VERSION}`).should('be.visible');
    cy.get('h2').should('have.length', 10);
  });

  it('mostra a política de privacidade como rascunho', () => {
    cy.visit('/privacidade');

    cy.contains('h1', 'Política de privacidade').should('be.visible');
    cy.get('[role="note"]').should('contain.text', 'não é aconselhamento jurídico');
  });
});
