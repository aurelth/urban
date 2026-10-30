import { MANAGER, stubAnonymous, stubSignedIn } from '../support/session';

const LOGO = 'a[aria-label="Urban, ir para a página inicial"]';

describe('Logo', () => {
  it('leva ao painel a partir de outra tela da área logada', () => {
    stubSignedIn();
    cy.visit('/conta');
    cy.location('pathname').should('eq', '/conta');

    cy.get('mat-toolbar').find(LOGO).click();

    cy.location('pathname').should('eq', '/dashboard');
  });

  it('leva o síndico que ainda não cadastrou o condomínio ao cadastro', () => {
    stubSignedIn({ ...MANAGER, condominiumId: null });
    cy.visit('/conta');

    cy.get('mat-toolbar').find(LOGO).click();

    cy.location('pathname').should('eq', '/condominio/cadastrar');
  });

  it('leva quem não entrou à tela de entrada, sem guardar a página inicial como destino', () => {
    stubAnonymous();
    cy.visit('/esqueci-a-senha');

    cy.get(LOGO).click();

    cy.location('pathname').should('eq', '/entrar');
    cy.location('search').should('eq', '');
  });

  it('volta da página de termos', () => {
    stubAnonymous();
    cy.visit('/termos');

    cy.get(LOGO).click();

    cy.location('pathname').should('eq', '/entrar');
    cy.location('search').should('eq', '');
  });
});
