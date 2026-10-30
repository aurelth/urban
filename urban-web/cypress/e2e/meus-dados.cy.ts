import { PALETTE } from '../support/layout';
import {
  PROFILE_PATH,
  stubProfile,
  stubProfileRejected,
  stubProfileSaves,
} from '../support/perfil';
import { stubPostalCodeFailure } from '../support/condominio';
import { stubSignedIn } from '../support/session';

function manager(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="manager-section"]');
}

function field(name: string): Cypress.Chainable<JQuery<HTMLElement>> {
  return manager().find(`[formControlName="${name}"]`);
}

function replace(name: string, value: string): void {
  field(name).click('right').type(`{selectall}${value}`);
}

describe('Meus dados', () => {
  beforeEach(() => {
    stubPostalCodeFailure(404, 'condominium.postal_code.not_found');
    stubSignedIn();
    stubProfile();
  });

  it('abre pelo link do cabecalho com os dados preenchidos e o CPF mascarado', () => {
    cy.visit('/dashboard');
    cy.get('[data-testid="nav-profile"]').click();

    cy.location('pathname').should('eq', PROFILE_PATH);
    cy.contains('h1', 'Meus dados').should('be.visible');
    field('fullName').should('have.value', 'Marina Souza');
    field('phone').should('have.value', '(47) 99123-4567');
    cy.get('[data-testid="cpf-masked"]').should('have.text', '***.982.247-**');
    cy.get('[data-testid="cnpj"]').should('contain.text', '11.222.333/0001-81');
    cy.contains('button', 'Salvar alterações').should('be.disabled');
  });

  it('edita o celular e o nome, salva sem pedir senha e avisa em verde', () => {
    stubProfileSaves('Mari');
    cy.visit(PROFILE_PATH);

    replace('displayName', 'Mari');
    replace('phone', '47988887766');
    cy.contains('button', 'Salvar alterações').click();

    cy.wait('@saveProfile').its('request.body').should('deep.include', {
      version: '7.9',
      displayName: 'Mari',
      phone: '47988887766',
    });
    cy.get('[data-testid="saved"]')
      .should('contain.text', 'Dados salvos')
      .find('.text-success mat-icon')
      .should('have.css', 'color', PALETTE.light.success);
    cy.get('[data-testid="current-user"]').should('contain.text', 'Olá Mari!');
    cy.get('urban-password-confirm-dialog').should('not.exist');
  });

  it('troca o CPF depois de confirmar a senha no dialogo', () => {
    stubProfileSaves('Marina', '***.444.777-**');
    cy.visit(PROFILE_PATH);

    cy.get('[data-testid="change-cpf"]').click();
    replace('cpf', '11144477735');
    cy.contains('button', 'Salvar alterações').click();
    // O elemento do diálogo é `display: contents`, então a visibilidade se confere num filho.
    cy.get('urban-password-confirm-dialog h2').should('be.visible');
    cy.get('urban-password-confirm-dialog input').type('Senha-segura-123');
    cy.get('urban-password-confirm-dialog').contains('button', 'Confirmar').click();

    cy.wait('@saveIdentity').its('request.body').should('deep.equal', {
      version: '7.9',
      password: 'Senha-segura-123',
      fullName: 'Marina Souza',
      birthDate: '1980-06-15',
      cpf: '11144477735',
    });
    cy.get('urban-password-confirm-dialog').should('not.exist');
    cy.get('[data-testid="saved"]').should('contain.text', 'Dados salvos');
    cy.get('[data-testid="cpf-masked"]').should('have.text', '***.444.777-**');
  });

  it('mostra a senha incorreta dentro do dialogo e continua logado', () => {
    stubProfileRejected(
      '/api/condominiums/mine/manager-identity',
      403,
      'identity.password.incorrect',
    );
    cy.visit(PROFILE_PATH);

    replace('fullName', 'Marina Souza Lima');
    cy.contains('button', 'Salvar alterações').click();
    cy.get('urban-password-confirm-dialog input').type('Senha-errada-999');
    cy.get('urban-password-confirm-dialog').contains('button', 'Confirmar').click();

    cy.wait('@saveIdentity');
    cy.get('urban-password-confirm-dialog [role="alert"]').should(
      'contain.text',
      'Senha incorreta',
    );
    cy.get('urban-password-confirm-dialog input').should('have.value', '');
    cy.location('pathname').should('eq', PROFILE_PATH);
    cy.get('[data-testid="current-user"]').should('contain.text', 'Olá Marina!');
  });

  it('avisa do conflito de versao e recarrega os dados quando a pessoa pede', () => {
    stubProfileRejected('/api/condominiums/mine', 409, 'concurrency.conflict');
    cy.visit(PROFILE_PATH);

    replace('phone', '47988887766');
    cy.contains('button', 'Salvar alterações').click();

    cy.get('[data-testid="conflict"]')
      .should('contain.text', 'Os dados foram alterados em outro lugar')
      .and('contain.text', 'Recarregar dados');
    cy.get('[data-testid="reload"]').click();
    cy.get('[data-testid="conflict"]').should('not.exist');
    field('phone').should('have.value', '(47) 99123-4567');
  });

  describe('no celular (375 x 667)', () => {
    beforeEach(() => {
      cy.viewport(375, 667);
    });

    it('cabe na tela, sem rolagem horizontal, mesmo com o dialogo aberto', () => {
      cy.visit(PROFILE_PATH);
      cy.contains('h1', 'Meus dados').should('be.visible');
      cy.document().should((doc) => {
        expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
      });

      replace('fullName', 'Marina Souza Lima');
      cy.contains('button', 'Salvar alterações').click();

      // O elemento do diálogo é `display: contents`, então a visibilidade se confere num filho.
      cy.get('urban-password-confirm-dialog h2').should('be.visible');
      cy.document().should((doc) => {
        expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
      });
    });

    it('mantem os links do cabecalho ao alcance', () => {
      cy.visit(PROFILE_PATH);

      cy.get('[data-testid="nav-profile"]').should('be.visible');
      cy.get('[data-testid="nav-account"]').should('be.visible');
      cy.get('[data-testid="sign-out"]').should('be.visible');
    });
  });

  for (const theme of ['light', 'dark'] as const) {
    it(`o aviso "Dados salvos" fica verde, com icone e texto, no tema ${theme}`, () => {
      stubProfileSaves('Mari');
      cy.visit(PROFILE_PATH, {
        onBeforeLoad: (win) => win.localStorage.setItem('urban.theme', theme),
      });

      replace('phone', '47988887766');
      cy.contains('button', 'Salvar alterações').click();

      cy.get('[data-testid="saved"] .text-success mat-icon')
        .should('exist')
        .and('have.css', 'color', PALETTE[theme].success);
      cy.get('[data-testid="saved"]').should('contain.text', 'Dados salvos');
    });
  }
});
