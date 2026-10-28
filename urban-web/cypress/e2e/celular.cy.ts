import { stubTerms } from '../support/cadastro';
import { MANAGER, stubAnonymous, stubSignedIn } from '../support/session';

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
    cy.visit('/confirmar-email?token=token-do-link');

    cy.contains('h1', 'Criar senha').should('be.visible');
    cy.get('input[autocomplete="new-password"]').should('be.visible');
    cy.get('mat-checkbox').should('be.visible');
    cy.contains('button', 'Ativar conta').should('be.visible');
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
