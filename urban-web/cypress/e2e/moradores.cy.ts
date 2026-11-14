import { chooseMenu } from '../support/menu';
import { DESKTOP, PHONE } from '../support/navegacao';
import { MANAGER, stubSignedIn } from '../support/session';
import { stubUnits } from '../support/unidades';
import {
  CURRENT_RESIDENT,
  RESIDENTS_PATH,
  stubInvitation,
  stubInvitationRejected,
  stubResident,
  stubResidents,
} from '../support/moradores';

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

  describe('acesso ao sistema', () => {
    it('mostra a situação do acesso e só oferece "Convidar" a quem tem e-mail e ainda não foi convidado', () => {
      stubResidents([
        CURRENT_RESIDENT,
        stubResident({ id: 'r-2', fullName: 'Caio Dias', accessStatus: 'invited' }),
        stubResident({ id: 'r-3', fullName: 'Dora Reis', accessStatus: 'active' }),
        stubResident({ id: 'r-4', fullName: 'Eva Melo', accessStatus: 'none', email: null }),
      ]);
      cy.visit(RESIDENTS_PATH);

      rows().should('have.length', 4);
      rows().eq(0).should('contain.text', 'Sem acesso').and('contain.text', 'Convidar');
      rows()
        .eq(1)
        .should('contain.text', 'Convite enviado')
        .and('contain.text', 'Reenviar convite');
      rows().eq(2).should('contain.text', 'Acesso ativo');
      rows()
        .eq(2)
        .find('[data-testid="invite"], [data-testid="resend-invite"]')
        .should('not.exist');
      rows().eq(3).should('contain.text', 'Sem acesso').and('not.contain.text', 'Convidar');
      rows().eq(3).find('[data-testid="access-hint"]').should('be.visible');
    });

    it('convida pela lista: a linha vira "Convite enviado" e oferece reenviar', () => {
      stubInvitation(CURRENT_RESIDENT);
      cy.visit(RESIDENTS_PATH);

      cy.get('[aria-label="Convidar Ana Lima"]').click();

      cy.wait('@invite');
      rows().first().should('contain.text', 'Convite enviado para ana@exemplo.com');
      rows()
        .first()
        .find('[data-testid="access-status"]')
        .should('contain.text', 'Convite enviado');
      rows().first().find('[data-testid="resend-invite"]').should('be.visible');
    });

    it('mostra o motivo quando a API recusa o convite e deixa tentar de novo', () => {
      stubInvitationRejected(CURRENT_RESIDENT, 409, 'identity.resident_access.email_unavailable');
      cy.visit(RESIDENTS_PATH);

      cy.get('[aria-label="Convidar Ana Lima"]').click();

      cy.wait('@invite');
      rows()
        .first()
        .find('[data-testid="access-error"]')
        .should('contain.text', 'já tem uma conta');
      rows().first().find('[data-testid="access-status"]').should('contain.text', 'Sem acesso');
      cy.get('[aria-label="Convidar Ana Lima"]').should('not.be.disabled');
    });

    it('cabe na tela do celular, sem rolagem horizontal, com a situação e o botão', () => {
      cy.viewport(PHONE.width, PHONE.height);
      cy.visit(RESIDENTS_PATH);

      rows().first().find('[data-testid="invite"]').should('be.visible');
      cy.document().should((doc) => {
        expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
      });
    });
  });
});
