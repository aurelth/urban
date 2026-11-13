import { PALETTE } from '../support/layout';
import { stubSignedIn } from '../support/session';

describe('Painel', () => {
  it('abre o painel na rota inicial', () => {
    stubSignedIn();
    cy.visit('/');

    cy.location('pathname').should('eq', '/dashboard');
    cy.get('h1').should('have.text', 'Painel').and('have.class', 'sr-only');
    cy.get('main').should('not.contain.text', 'Gestão condominial');
    cy.get('mat-card.module').should('have.length', 3);
  });

  it('cumprimenta pelo nome escolhido e mostra o papel no cabeçalho', () => {
    stubSignedIn();
    cy.visit('/dashboard');

    cy.get('[data-testid="current-user"]')
      .should('have.text', 'Olá Marina - Síndico')
      .and('not.contain.text', '!')
      .and('not.contain.text', 'sindica@example.com');
  });

  it('separa o cabeçalho do corpo com uma linha fina, só para ele ficar levemente visível', () => {
    stubSignedIn();
    cy.visit('/dashboard', {
      onBeforeLoad: (win) => win.localStorage.setItem('urban.theme', 'light'),
    });

    cy.get('mat-toolbar')
      .should('have.css', 'border-bottom-width', '1px')
      .and('have.css', 'border-bottom-style', 'solid')
      .and('have.css', 'border-bottom-color', 'rgb(230, 230, 239)')
      .and('have.css', 'border-top-width', '0px');
    cy.get('mat-toolbar').should('have.css', 'background-color', PALETTE.light.card);
  });

  it('mantém a linha do cabeçalho no tema escuro, na cor das bordas', () => {
    stubSignedIn();
    cy.visit('/dashboard', {
      onBeforeLoad: (win) => win.localStorage.setItem('urban.theme', 'dark'),
    });

    cy.get('html').should('have.class', 'app-dark');
    cy.get('mat-toolbar')
      .should('have.css', 'border-bottom-width', '1px')
      .and('have.css', 'border-bottom-color', 'rgb(44, 44, 46)');
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
