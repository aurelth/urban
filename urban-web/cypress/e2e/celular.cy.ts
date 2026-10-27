import { MANAGER, stubAnonymous, stubSignedIn } from '../support/session';

function expectNoHorizontalScroll(): void {
  cy.document().then((doc) => {
    expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
  });
}

describe('Tela de celular (375 x 667)', () => {
  beforeEach(() => {
    cy.viewport(375, 667);
  });

  it('mostra a entrada inteira, sem rolagem horizontal', () => {
    stubAnonymous();
    cy.visit('/entrar');

    cy.contains('h1', 'Entrar').should('be.visible');
    cy.get('input[type="email"]').should('be.visible');
    cy.contains('button', 'Entrar').should('be.visible');
    expectNoHorizontalScroll();
  });

  it('mostra o painel em coluna única, com a saída à mão e sem rolagem horizontal', () => {
    stubSignedIn(MANAGER);
    cy.visit('/dashboard');

    cy.get('[data-testid="sign-out"]').should('be.visible');
    cy.get('mat-card.module').should('have.length', 3).and('be.visible');
    cy.get('mat-card.module').then(($cards) => {
      const lefts = [...$cards].map((card) => card.getBoundingClientRect().left);
      expect(new Set(lefts).size).to.eq(1);
    });
    expectNoHorizontalScroll();
  });
});
