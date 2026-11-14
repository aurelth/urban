import { PALETTE, expectErrorsToStayInTheirOwnSpace } from '../support/layout';
import { chooseMenu } from '../support/menu';
import { DESKTOP } from '../support/navegacao';
import { MANAGER, stubSignedIn } from '../support/session';
import { SECOND_UNIT, stubUnits } from '../support/unidades';
import {
  FIRST_VEHICLE,
  SECOND_VEHICLE,
  VEHICLES_PATH,
  stubVehicle,
  stubVehicleRejected,
  stubVehicles,
} from '../support/veiculos';

function rows(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="vehicle-row"]');
}

/** O rótulo do Material cobre o meio do campo vazio, então o clique vai para a direita (como nas unidades). */
function enter(name: string, value: string): void {
  cy.get(`urban-vehicle-dialog [formControlName="${name}"]`)
    .click('right')
    .type(`{selectall}${value}`);
}

describe('Veículos', () => {
  beforeEach(() => {
    stubSignedIn(MANAGER);
    stubUnits();
    stubVehicles();
  });

  it('abre pelo menu lateral com a lista de veículos e suas unidades', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit('/dashboard');
    cy.get('[data-testid="nav-vehicles"]').click();

    cy.location('pathname').should('eq', VEHICLES_PATH);
    cy.contains('h1', 'Veículos').should('be.visible');
    rows().should('have.length', 2);
    rows()
      .first()
      .should('contain.text', 'ABC1D23')
      .and('contain.text', 'Fiat Argo')
      .and('contain.text', 'Prata')
      .and('contain.text', 'Bloco A · 101');
  });

  it('cadastra um veículo pelo diálogo, com a placa em maiúsculas, e avisa em verde', () => {
    cy.intercept('POST', '/api/vehicles', { statusCode: 201, body: stubVehicle() }).as('create');
    cy.visit(VEHICLES_PATH);

    cy.get('[data-testid="new-vehicle"]').click();
    cy.get('urban-vehicle-dialog h2').should('contain.text', 'Novo veículo');
    chooseMenu(cy.get('urban-vehicle-dialog urban-menu-select'), 'Bloco A · 102');
    enter('plate', 'def-2g34');
    cy.get('urban-vehicle-dialog [formControlName="plate"]').should('have.value', 'DEF2G34');
    enter('model', 'VW Gol');
    enter('color', 'Branco');
    cy.get('urban-vehicle-dialog').contains('button', 'Cadastrar veículo').click();

    cy.wait('@create').its('request.body').should('deep.equal', {
      unitId: SECOND_UNIT.id,
      plate: 'DEF2G34',
      model: 'VW Gol',
      color: 'Branco',
    });
    cy.get('urban-vehicle-dialog').should('not.exist');
    cy.get('@vehicles.all').should('have.length.at.least', 2);
    cy.get('[data-testid="notice"]')
      .should('contain.text', 'Veículo DEF2G34 cadastrado')
      .find('.text-success mat-icon')
      .should('have.css', 'color', PALETTE.light.success);
  });

  it('mostra os erros dos campos do diálogo sem cobrir nada', () => {
    cy.visit(VEHICLES_PATH);

    cy.get('[data-testid="new-vehicle"]').click();
    enter('plate', 'ABC12');
    cy.get('urban-vehicle-dialog').contains('button', 'Cadastrar veículo').click();

    cy.get('urban-vehicle-dialog').should('contain.text', 'Escolha a unidade');
    cy.get('urban-vehicle-dialog').should('contain.text', 'Confira a placa');
    cy.get('urban-vehicle-dialog').should('contain.text', 'Informe o modelo');
    cy.get('urban-vehicle-dialog').should('contain.text', 'Informe a cor');
    expectErrorsToStayInTheirOwnSpace();
  });

  it('mostra no diálogo que a placa já está cadastrada', () => {
    stubVehicleRejected('POST', '/api/vehicles', 409, 'vehicle.plate.already_registered', 'create');
    cy.visit(VEHICLES_PATH);

    cy.get('[data-testid="new-vehicle"]').click();
    chooseMenu(cy.get('urban-vehicle-dialog urban-menu-select'), 'Bloco A · 101');
    enter('plate', 'ABC1D23');
    enter('model', 'Fiat Argo');
    enter('color', 'Prata');
    cy.get('urban-vehicle-dialog').contains('button', 'Cadastrar veículo').click();

    cy.get('urban-vehicle-dialog [role="alert"]').should(
      'contain.text',
      'Já existe um veículo com esta placa neste condomínio',
    );
  });

  it('edita um veículo levando a versão lida', () => {
    cy.intercept('PUT', `/api/vehicles/${FIRST_VEHICLE.id}`, {
      statusCode: 200,
      body: { ...FIRST_VEHICLE, color: 'Preto', version: '23' },
    }).as('update');
    cy.visit(VEHICLES_PATH);

    cy.get('button[aria-label="Editar veículo ABC1D23"]').click();
    cy.get('urban-vehicle-dialog [formControlName="color"]').should('have.value', 'Prata');
    enter('color', 'Preto');
    cy.get('urban-vehicle-dialog').contains('button', 'Salvar alterações').click();

    cy.wait('@update').its('request.body').should('deep.equal', {
      unitId: FIRST_VEHICLE.unitId,
      plate: 'ABC1D23',
      model: 'Fiat Argo',
      color: 'Preto',
      version: '21',
    });
    cy.get('urban-vehicle-dialog').should('not.exist');
    cy.get('[data-testid="notice"]').should('contain.text', 'Veículo ABC1D23 atualizado');
  });

  it('filtra por unidade e busca por texto', () => {
    cy.visit(VEHICLES_PATH);
    rows().should('have.length', 2);

    stubVehicles([SECOND_VEHICLE], 'byUnit');
    chooseMenu('[data-testid="filter-unit"]', 'Bloco A · 102');
    cy.wait('@byUnit').its('request.url').should('contain', `unitId=${SECOND_UNIT.id}`);
    rows().should('have.length', 1);

    stubVehicles([], 'bySearch');
    cy.get('[data-testid="filter-search"]').click('right').type('zzz{enter}').blur();
    cy.wait('@bySearch').its('request.url').should('contain', 'search=zzz');
    cy.get('[data-testid="no-results"]').should(
      'contain.text',
      'Nenhum veículo encontrado com estes filtros',
    );
  });

  it('pede confirmação antes de excluir e explica que é definitivo', () => {
    cy.intercept('DELETE', `/api/vehicles/${FIRST_VEHICLE.id}`, { statusCode: 204 }).as('remove');
    cy.visit(VEHICLES_PATH);

    cy.get('button[aria-label="Excluir veículo ABC1D23"]').click();
    cy.get('[data-testid="delete-confirm"]')
      .should('contain.text', 'Excluir o veículo ABC1D23?')
      .and('contain.text', 'definitiva');
    cy.get('[data-testid="cancel-delete"]').click();
    cy.get('[data-testid="delete-confirm"]').should('not.exist');

    cy.get('button[aria-label="Excluir veículo ABC1D23"]').click();
    stubVehicles([SECOND_VEHICLE], 'afterDelete');
    cy.get('[data-testid="confirm-delete"]').click();

    cy.wait('@remove');
    cy.wait('@afterDelete');
    rows().should('have.length', 1);
    cy.get('[data-testid="notice"]').should('contain.text', 'Veículo ABC1D23 excluído');
  });

  it('avisa quando o veículo já não existe', () => {
    stubVehicleRejected(
      'DELETE',
      `/api/vehicles/${FIRST_VEHICLE.id}`,
      404,
      'vehicle.not_found',
      'remove',
    );
    cy.visit(VEHICLES_PATH);

    cy.get('button[aria-label="Excluir veículo ABC1D23"]').click();
    cy.get('[data-testid="confirm-delete"]').click();

    cy.get('[data-testid="action-error"]').should(
      'contain.text',
      'Este veículo não existe mais. A lista já foi atualizada',
    );
  });

  it('sem nenhum veículo, convida a cadastrar', () => {
    stubVehicles([]);
    cy.visit(VEHICLES_PATH);

    cy.get('[data-testid="empty"]').should('contain.text', 'Nenhum veículo cadastrado ainda');
  });

  it('sem unidade ativa, leva ao cadastro de unidades', () => {
    stubUnits([]);
    stubVehicles([]);
    cy.visit(VEHICLES_PATH);

    cy.get('[data-testid="no-units"]').should('contain.text', 'Cadastre uma unidade');
    cy.get('[data-testid="new-vehicle"]').should('not.exist');
    cy.get('[data-testid="no-units-link"]').should('have.attr', 'href', '/unidades');
  });

  it('manda moradores de volta ao painel', () => {
    stubSignedIn({ ...MANAGER, role: 'Resident' });
    cy.visit(VEHICLES_PATH);

    cy.location('pathname').should('eq', '/dashboard');
  });

  it('cabe numa tela de celular (375 x 667), com a confirmação de exclusão, sem rolagem horizontal', () => {
    cy.viewport(375, 667);
    cy.visit(VEHICLES_PATH);

    rows().should('have.length', 2);
    cy.get('[data-testid="user-menu"]').should('be.visible');
    cy.get('button[aria-label="Excluir veículo ABC1D23"]').should('be.visible').click();
    cy.get('[data-testid="delete-confirm"]').should('be.visible');
    cy.document().should((doc) => {
      expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
    });
  });
});
