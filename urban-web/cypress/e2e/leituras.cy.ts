import { ENTRIES_PATH, TODAY, stubFinanceRejected, stubMonth } from '../support/financeiro';
import { expectErrorsToStayInTheirOwnSpace } from '../support/layout';
import {
  OPEN_READINGS,
  READINGS_PATH,
  UNIT_101,
  UNIT_102,
  readingField,
  readingsApi,
  stubReadings,
} from '../support/leituras';
import { stubSignedIn } from '../support/session';

/** O rótulo do Material cobre o meio do campo vazio, então o clique vai para a direita (como em "Meus dados"). */
function enter(field: Cypress.Chainable<JQuery<HTMLElement>>, value: string): void {
  const clicked = field.click('right');
  if (value === '') {
    clicked.clear();
  } else {
    clicked.type(`{selectall}${value}`);
  }
}

function rows(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="unit-row"]');
}

describe('Leituras do mês', () => {
  beforeEach(() => {
    cy.clock(TODAY, ['Date']);
    stubSignedIn();
    stubReadings();
  });

  it('abre pelo link da tela de lançamentos, no mesmo mês', () => {
    stubMonth();
    cy.visit(ENTRIES_PATH);

    cy.get('[data-testid="readings-link"]').click();

    cy.location('pathname').should('eq', READINGS_PATH);
    cy.location('search').should('eq', '?mes=2026-11');
    cy.contains('h1', 'Leituras do mês').should('be.visible');
    cy.get('[data-testid="month-title"]').should('contain.text', 'Novembro de 2026');
    rows().should('have.length', 3);
  });

  it('mostra as unidades com a leitura do mês passado como anterior', () => {
    cy.visit(READINGS_PATH);

    rows().first().should('contain.text', 'Bloco A · 101');
    readingField(0, 'waterPrevious').should('have.value', '100');
    readingField(0, 'waterCurrent').should('have.value', '');
    readingField(1, 'waterCurrent').should('have.value', '60');
    rows().eq(1).find('[data-testid="water-consumption"]').should('contain.text', '10 m³');
    cy.get('[data-testid="coverage-line"]').should(
      'contain.text',
      'Água: 1 de 3 unidades com leitura',
    );
  });

  it('calcula o consumo ao digitar e grava só o que mudou', () => {
    cy.intercept('PUT', readingsApi(), { statusCode: 200, body: OPEN_READINGS }).as('save');
    cy.visit(READINGS_PATH);

    cy.get('[data-testid="save-readings"]').should('be.disabled');
    enter(readingField(0, 'waterCurrent'), '112,5');
    rows().first().find('[data-testid="water-consumption"]').should('contain.text', '12,5 m³');
    cy.get('[data-testid="dirty-notice"]').should('contain.text', 'Há alterações não salvas');
    cy.get('[data-testid="save-readings"]').click();

    cy.wait('@save')
      .its('request.body')
      .should('deep.equal', {
        readings: [{ unitId: UNIT_101.unitId, service: 'water', previous: 100, current: 112.5 }],
      });
    cy.get('[data-testid="notice"]').should('contain.text', 'Leituras salvas');
    cy.get('[data-testid="save-readings"]').should('be.disabled');
  });

  it('retira uma leitura que foi apagada', () => {
    cy.intercept('PUT', readingsApi(), { statusCode: 200, body: OPEN_READINGS }).as('save');
    cy.visit(READINGS_PATH);

    enter(readingField(1, 'waterCurrent'), '');
    cy.get('[data-testid="save-readings"]').click();

    cy.wait('@save')
      .its('request.body.readings')
      .should('deep.equal', [{ unitId: UNIT_102.unitId, service: 'water', current: null }]);
  });

  it('explica o campo inválido e a leitura menor que a anterior, sem cobrir outros campos', () => {
    cy.visit(READINGS_PATH);

    enter(readingField(0, 'waterCurrent'), '99');
    cy.get('[data-testid="water-order-error"]').should(
      'contain.text',
      'A leitura atual não pode ser menor que a anterior',
    );
    enter(readingField(2, 'waterCurrent'), 'abc');
    cy.get('[data-testid="save-readings"]').click();

    cy.contains('mat-error', 'Informe a leitura atual em m³, com até 3 casas decimais').should(
      'be.visible',
    );
    expectErrorsToStayInTheirOwnSpace();
  });

  it('descarta as leituras digitadas', () => {
    cy.visit(READINGS_PATH);

    enter(readingField(0, 'waterCurrent'), '112,5');
    cy.get('[data-testid="discard-readings"]').click();

    readingField(0, 'waterCurrent').should('have.value', '');
    cy.get('[data-testid="dirty-notice"]').should('not.exist');
  });

  it('mostra o erro da API sem perder o que foi digitado', () => {
    stubFinanceRejected('PUT', readingsApi(), 400, 'consumption.reading_invalid', 'save');
    cy.visit(READINGS_PATH);

    enter(readingField(0, 'waterCurrent'), '112,5');
    cy.get('[data-testid="save-readings"]').click();

    cy.get('[data-testid="action-error"]').should(
      'contain.text',
      'A leitura atual não pode ser menor',
    );
    readingField(0, 'waterCurrent').should('have.value', '112,5');
  });

  it('mostra as leituras de gás quando a tarifa cobra gás', () => {
    stubReadings({ ...OPEN_READINGS, hasGas: true });
    cy.visit(READINGS_PATH);

    readingField(0, 'gasPrevious').should('have.value', '7');
    cy.get('[data-testid="coverage-line"]').should('have.length', 2);
  });

  it('avisa quando não há tarifa em vigor no mês', () => {
    stubReadings({ ...OPEN_READINGS, hasTariff: false });
    cy.visit(READINGS_PATH);

    cy.get('[data-testid="no-tariff"]').should(
      'contain.text',
      'Não há tarifa de consumo em vigor neste mês',
    );
  });

  it('trava as leituras do mês fechado', () => {
    stubReadings({ ...OPEN_READINGS, isClosed: true });
    cy.visit(READINGS_PATH);

    cy.get('[data-testid="closed-notice"]').should('contain.text', 'Este mês está fechado');
    readingField(1, 'waterCurrent').should('be.disabled');
    cy.get('[data-testid="save-readings"]').should('not.exist');
  });

  it('troca de mês, mas só depois de salvar ou descartar as leituras', () => {
    stubReadings({ ...OPEN_READINGS, month: '2026-12', units: [UNIT_101] }, '2026-12');
    cy.visit(READINGS_PATH);

    enter(readingField(0, 'waterCurrent'), '112,5');
    cy.get('button[aria-label="Próximo mês"]').should('be.disabled');
    cy.get('[data-testid="discard-readings"]').click();
    cy.get('button[aria-label="Próximo mês"]').click();

    cy.get('[data-testid="month-title"]').should('contain.text', 'Dezembro de 2026');
    rows().should('have.length', 1);
  });

  it('cabe numa tela de 360 px, com os campos de cada unidade inteiros e sem rolagem horizontal', () => {
    cy.viewport(360, 640);
    stubReadings({ ...OPEN_READINGS, hasGas: true });
    cy.visit(READINGS_PATH);

    rows().should('have.length', 3);
    cy.document().should((doc) => {
      expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
    });
    cy.get('[data-testid="unit-row"] mat-form-field').each(($field) => {
      const { left, right } = $field[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(360);
    });
  });
});
