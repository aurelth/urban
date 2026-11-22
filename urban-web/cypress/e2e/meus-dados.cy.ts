import { PALETTE } from '../support/layout';
import { DESKTOP, chooseUserMenu, openUserMenu } from '../support/navegacao';
import {
  PROFILE_PATH,
  stubProfile,
  stubProfileRejected,
  stubProfileSaves,
} from '../support/perfil';
import { stubPostalCodeFailure } from '../support/condominio';
import { chooseMenu } from '../support/menu';
import {
  MANAGER_REGISTRATION,
  MY_CURRENT_OCCUPANCY,
  stubMyRegistration,
  stubMyRegistrationRejected,
} from '../support/meu-cadastro';
import { MY_ACCESS_API } from '../support/minha-fatura';
import { typeInto } from '../support/moradores';
import { stubSignedIn } from '../support/session';
import { FIRST_UNIT, stubUnits } from '../support/unidades';

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

  it('abre pelo menu do avatar com os dados preenchidos e o CPF mascarado', () => {
    cy.visit('/dashboard');
    chooseUserMenu('menu-profile');

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
    cy.get('[data-testid="current-user"]').should('contain.text', 'Olá Mari - Síndico');
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
    cy.get('[data-testid="current-user"]').should('contain.text', 'Olá Marina');
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

    it('mantem o menu do avatar ao alcance', () => {
      cy.visit(PROFILE_PATH);

      openUserMenu();
      cy.get('[data-testid="menu-profile"]').should('be.visible');
      cy.get('[data-testid="menu-account"]').should('be.visible');
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

  describe('onde eu moro', () => {
    beforeEach(() => {
      stubUnits();
    });

    it('mostra o cartão e diz que o síndico ainda não mora em nenhuma unidade', () => {
      cy.visit(PROFILE_PATH);

      cy.get('[data-testid="occupancies"] h2').should('have.text', 'Onde eu moro');
      cy.get('[data-testid="no-occupancies"]').should(
        'contain.text',
        'Você ainda não mora em nenhuma unidade',
      );
      cy.get('[data-testid="manager-section"]').should('exist');
    });

    it('mostra a unidade onde o síndico mora', () => {
      stubMyRegistration({ ...MANAGER_REGISTRATION, occupancies: [MY_CURRENT_OCCUPANCY] });
      cy.visit(PROFILE_PATH);

      cy.get('[data-testid="occupancy"]')
        .should('have.length', 1)
        .and('contain.text', 'Bloco A · 101')
        .and('contain.text', 'Proprietário, desde 01/03/2024');
    });

    it('avisa só no cartão quando o cadastro próprio não carrega', () => {
      stubMyRegistrationRejected(500, 'internal');
      cy.visit(PROFILE_PATH);

      cy.get('[data-testid="housing-error"]').should(
        'contain.text',
        'Não foi possível carregar onde você mora',
      );
      cy.get('[data-testid="manager-section"]').should('exist');
    });

    it('cadastrar a unidade faz "Minha fatura" aparecer no menu, sem entrar de novo', () => {
      cy.viewport(DESKTOP.width, DESKTOP.height);
      let access = 'none';
      cy.intercept('GET', MY_ACCESS_API, (request) => {
        request.reply({ statusCode: 200, body: { access } });
      });
      cy.intercept('POST', `/api/residents/${MANAGER_REGISTRATION.id}/occupancies`, (request) => {
        access = 'unit';
        request.reply({
          statusCode: 201,
          body: {
            ...MANAGER_REGISTRATION,
            occupancies: [{ ...MY_CURRENT_OCCUPANCY, startDate: '2026-01-05' }],
          },
        });
      }).as('addOccupancy');
      cy.visit(PROFILE_PATH);
      cy.get('[data-testid="no-occupancies"]').should('exist');
      cy.get('[data-testid="nav-my-invoice"]').should('not.exist');

      cy.get('[data-testid="add-occupancy"]').click();
      chooseMenu('[formControlName="unitId"]', 'Bloco A · 101');
      chooseMenu('[formControlName="kind"]', 'Proprietário');
      typeInto('startDate', '05012026');
      cy.contains('button[type="submit"]', 'Adicionar vínculo').click();

      cy.wait('@addOccupancy')
        .its('request.body')
        .should('deep.equal', { unitId: FIRST_UNIT.id, kind: 'owner', startDate: '2026-01-05' });
      cy.get('[data-testid="occupancy-notice"]').should(
        'contain.text',
        'Vínculo com Bloco A · 101 adicionado',
      );
      cy.get('[data-testid="nav-my-invoice"]').should('be.visible');
    });

    it('registrar a saída faz "Minha fatura" sumir do menu', () => {
      cy.viewport(DESKTOP.width, DESKTOP.height);
      stubMyRegistration({ ...MANAGER_REGISTRATION, occupancies: [MY_CURRENT_OCCUPANCY] });
      let access = 'unit';
      cy.intercept('GET', MY_ACCESS_API, (request) => {
        request.reply({ statusCode: 200, body: { access } });
      });
      cy.intercept(
        'POST',
        `/api/residents/${MANAGER_REGISTRATION.id}/occupancies/${MY_CURRENT_OCCUPANCY.id}/end`,
        (request) => {
          access = 'none';
          request.reply({
            statusCode: 200,
            body: {
              ...MANAGER_REGISTRATION,
              occupancies: [{ ...MY_CURRENT_OCCUPANCY, endDate: '2026-11-10' }],
            },
          });
        },
      ).as('endOccupancy');
      cy.visit(PROFILE_PATH);
      cy.get('[data-testid="nav-my-invoice"]').should('be.visible');

      cy.get('[data-testid="end-occupancy"]').click();
      typeInto('endDate', '10112026');
      cy.contains('button[type="submit"]', 'Registrar saída').click();

      cy.wait('@endOccupancy').its('request.body').should('deep.equal', { endDate: '2026-11-10' });
      cy.get('[data-testid="nav-my-invoice"]').should('not.exist');
    });
  });
});
