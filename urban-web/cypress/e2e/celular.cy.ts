import { stubTerms } from '../support/cadastro';
import { NEW_MANAGER } from '../support/condominio';
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
    cy.visit('/dashboard');

    cy.get('[data-testid="sign-out"]').should('be.visible');
    cy.get('mat-card.module').should('have.length', 3).and('be.visible');
    cy.get('mat-card.module').then(($cards) => {
      const lefts = [...$cards].map((card) => card.getBoundingClientRect().left);
      expect(new Set(lefts).size).to.eq(1);
    });
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
