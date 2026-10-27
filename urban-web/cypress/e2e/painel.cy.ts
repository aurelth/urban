import { MANAGER, stubSignedIn } from '../support/session';

describe('Painel', () => {
  it('abre o painel na rota inicial', () => {
    stubSignedIn();
    cy.visit('/');

    cy.location('pathname').should('eq', '/dashboard');
    cy.get('h1').should('have.text', 'Painel').and('have.class', 'sr-only');
    cy.get('main').should('not.contain.text', 'Gestão condominial');
    cy.get('mat-card.module').should('have.length', 3);
    cy.get('[data-testid="condominium-notice"]').should('not.exist');
  });

  it('avisa o síndico que o condomínio ainda não foi cadastrado', () => {
    stubSignedIn({ ...MANAGER, condominiumId: null });
    cy.visit('/dashboard');

    cy.get('[data-testid="condominium-notice"]')
      .should('be.visible')
      .and('contain.text', 'O cadastro do seu condomínio ainda não foi feito');
  });

  it('não separa o cabeçalho do corpo com uma linha', () => {
    stubSignedIn();
    cy.visit('/dashboard');

    cy.get('mat-toolbar').should('have.css', 'border-bottom-width', '0px');
  });

  it('alterna entre tema claro e escuro', () => {
    stubSignedIn();
    // Parte sempre do tema claro, independente da preferencia do sistema de quem roda o teste.
    cy.visit('/', {
      onBeforeLoad: (win) => win.localStorage.setItem('urban.theme', 'light'),
    });
    cy.get('html').should('not.have.class', 'app-dark');

    cy.get('button[aria-label="Usar tema escuro"]').click();
    cy.get('html').should('have.class', 'app-dark');

    cy.get('button[aria-label="Usar tema claro"]').click();
    cy.get('html').should('not.have.class', 'app-dark');
  });
});
