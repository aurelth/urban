import { chooseMenu } from '../support/menu';
import { DESKTOP, PHONE } from '../support/navegacao';
import { MANAGER, stubSignedIn } from '../support/session';
import { stubUnits } from '../support/unidades';
import { RESIDENTS_PATH, stubResidents } from '../support/moradores';

function rows(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="resident-row"]');
}

describe('Moradores', () => {
  beforeEach(() => {
    stubSignedIn(MANAGER);
    stubUnits();
    stubResidents();
  });

  it('abre pelo menu lateral e mostra só quem mora, com a unidade e desde quando', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit('/dashboard');
    cy.get('[data-testid="nav-residents"]').click();

    cy.location('pathname').should('eq', RESIDENTS_PATH);
    cy.contains('h1', 'Moradores').should('be.visible');
    rows().should('have.length', 1);
    rows().first().should('contain.text', 'Ana Lima').and('contain.text', '***.123.456-**');
    rows().first().should('contain.text', '(47) 99876-5432').and('contain.text', 'ana@exemplo.com');
    rows().first().should('contain.text', 'Bloco A · 101');
    rows().first().should('contain.text', 'Proprietário, desde 01/03/2024');
    rows().first().should('contain.text', 'Mora');
    cy.wait('@residents').its('request.url').should('contain', 'status=active');
  });

  it('troca para ex-moradores e mostra o período em que moraram', () => {
    cy.visit(RESIDENTS_PATH);
    rows().should('have.length', 1);

    chooseMenu('[data-testid="filter-status"]', 'Ex-moradores');

    rows().should('have.length', 1);
    rows().first().should('contain.text', 'Beto Rocha');
    rows().first().should('contain.text', 'Inquilino, de 10/01/2023 a 31/10/2026');
    rows().first().should('contain.text', 'Ex-morador').and('contain.text', 'Sem e-mail');
    cy.get('@residents.all').its('length').should('be.at.least', 2);
  });

  it('mostra todos e busca pelo nome', () => {
    cy.visit(RESIDENTS_PATH);

    chooseMenu('[data-testid="filter-status"]', 'Todos');
    rows().should('have.length', 2);

    cy.get('[data-testid="filter-search"]').click('right').type('beto{enter}');
    cy.get('[data-testid="filter-search"]').blur();
    cy.get('@residents.all').then((calls) => {
      const last = calls[calls.length - 1] as unknown as { request: { url: string } };
      expect(last.request.url).to.contain('search=beto');
      expect(last.request.url).to.contain('status=all');
    });
  });

  it('no celular cabe na tela, sem rolagem horizontal, e o contato fica sob o nome', () => {
    cy.viewport(PHONE.width, PHONE.height);
    cy.visit(RESIDENTS_PATH);

    rows().should('have.length', 1);
    cy.document().should((doc) => {
      expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
    });
    rows().first().should('contain.text', '(47) 99876-5432');
  });
});
