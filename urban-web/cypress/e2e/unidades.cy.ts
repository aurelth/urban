import { chooseMenu } from '../support/menu';
import { DESKTOP } from '../support/navegacao';
import { PALETTE, expectErrorsToStayInTheirOwnSpace } from '../support/layout';
import { MANAGER, stubSignedIn } from '../support/session';
import {
  BATCH_PATH,
  DEACTIVATED_UNIT,
  FIRST_UNIT,
  SECOND_UNIT,
  UNITS_PATH,
  stubSummary,
  stubUnit,
  stubUnitRejected,
  stubUnits,
} from '../support/unidades';

function rows(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="unit-row"]');
}

/** O rótulo do Material cobre o meio do campo vazio, então o clique vai para a direita (como em "Meus dados"). */
function enter(selector: string, value: string): void {
  cy.get(selector).click('right').type(`{selectall}${value}`);
}

describe('Unidades', () => {
  beforeEach(() => {
    stubSignedIn();
    stubUnits();
  });

  it('abre pelo menu lateral com a lista, o resumo e o metodo de rateio', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit('/dashboard');
    cy.get('[data-testid="nav-units"]').click();

    cy.location('pathname').should('eq', UNITS_PATH);
    cy.contains('h1', 'Unidades').should('be.visible');
    rows().should('have.length', 2);
    rows().first().should('contain.text', 'Bloco A · 101').and('contain.text', '1,25%');
    rows().first().should('contain.text', '52,5 m²');
    cy.get('[data-testid="summary-active"]').should('contain.text', '2');
    cy.get('[data-testid="summary-without-area"]').should('contain.text', '1');
    cy.get('input[type="radio"][value="idealFraction"]').should('be.checked');
    cy.get('[data-testid="readiness-equalShare"]').should('contain.text', 'Pronto para usar');
  });

  it('cadastra uma unidade pelo dialogo e atualiza a lista', () => {
    cy.intercept('POST', '/api/units', { statusCode: 201, body: stubUnit() }).as('createUnit');
    cy.visit(UNITS_PATH);

    cy.get('[data-testid="new-unit"]').click();
    cy.get('urban-unit-dialog h2').should('contain.text', 'Nova unidade');
    enter('urban-unit-dialog [formControlName="block"]', 'B');
    enter('urban-unit-dialog [formControlName="number"]', '301');
    enter('urban-unit-dialog [formControlName="idealFraction"]', '1,5');
    enter('urban-unit-dialog [formControlName="area"]', '48,75');
    cy.get('urban-unit-dialog').contains('button', 'Cadastrar unidade').click();

    cy.wait('@createUnit').its('request.body').should('deep.equal', {
      block: 'B',
      number: '301',
      type: 'apartment',
      idealFraction: 0.015,
      area: 48.75,
    });
    cy.get('urban-unit-dialog').should('not.exist');
    cy.get('@units.all').should('have.length.at.least', 2);
  });

  it('mostra os erros dos campos do dialogo sem cobrir nada', () => {
    cy.visit(UNITS_PATH);

    cy.get('[data-testid="new-unit"]').click();
    cy.get('urban-unit-dialog [formControlName="block"]').should('be.focused');
    enter('urban-unit-dialog [formControlName="idealFraction"]', '150');
    cy.get('urban-unit-dialog').contains('button', 'Cadastrar unidade').click();

    cy.get('urban-unit-dialog').should('contain.text', 'Informe o número da unidade');
    cy.get('urban-unit-dialog').should('contain.text', 'Informe uma fração maior que 0%');
    expectErrorsToStayInTheirOwnSpace();
  });

  it('mostra no dialogo que a unidade ja existe', () => {
    stubUnitRejected('POST', '/api/units', 409, 'unit.identifier.already_exists', 'createUnit');
    cy.visit(UNITS_PATH);

    cy.get('[data-testid="new-unit"]').click();
    enter('urban-unit-dialog [formControlName="number"]', '101');
    cy.get('urban-unit-dialog').contains('button', 'Cadastrar unidade').click();

    cy.get('urban-unit-dialog [role="alert"]').should(
      'contain.text',
      'Já existe uma unidade com este bloco e número',
    );
  });

  it('edita uma unidade levando a versao lida', () => {
    cy.intercept('PUT', `/api/units/${FIRST_UNIT.id}`, {
      statusCode: 200,
      body: { ...FIRST_UNIT, area: 60, version: '44' },
    }).as('updateUnit');
    cy.visit(UNITS_PATH);

    cy.get('button[aria-label="Editar Bloco A · 101"]').click();
    cy.get('urban-unit-dialog [formControlName="area"]').should('have.value', '52,5');
    enter('urban-unit-dialog [formControlName="area"]', '60');
    cy.get('urban-unit-dialog').contains('button', 'Salvar alterações').click();

    cy.wait('@updateUnit').its('request.body').should('deep.equal', {
      block: 'A',
      number: '101',
      type: 'apartment',
      idealFraction: 0.0125,
      area: 60,
      version: '41',
    });
    cy.get('urban-unit-dialog').should('not.exist');
  });

  it('desativa sem confirmacao e avisa em verde com texto', () => {
    cy.intercept('POST', `/api/units/${FIRST_UNIT.id}/deactivate`, {
      statusCode: 200,
      body: { ...FIRST_UNIT, isActive: false },
    }).as('deactivate');
    cy.visit(UNITS_PATH);

    cy.get('button[aria-label="Desativar Bloco A · 101"]').click();

    cy.wait('@deactivate').its('request.body').should('deep.equal', { version: '41' });
    cy.get('[data-testid="notice"]')
      .should('contain.text', 'Bloco A · 101 desativada')
      .find('.text-success mat-icon')
      .should('have.css', 'color', PALETTE.light.success);
  });

  it('filtra pelas desativadas e reativa uma unidade', () => {
    cy.intercept('POST', `/api/units/${DEACTIVATED_UNIT.id}/reactivate`, {
      statusCode: 200,
      body: { ...DEACTIVATED_UNIT, isActive: true },
    }).as('reactivate');
    cy.visit(UNITS_PATH);
    rows().should('have.length', 2);

    stubUnits([DEACTIVATED_UNIT], stubSummary(), 'deactivatedUnits');
    chooseMenu('[data-testid="filter-status"]', 'Desativadas');

    cy.wait('@deactivatedUnits').its('request.url').should('contain', 'status=deactivated');
    rows().should('have.length', 1).and('contain.text', 'Desativada');
    cy.get('button[aria-label="Reativar Bloco B · 201"]').click();
    cy.wait('@reactivate').its('request.body').should('deep.equal', { version: '43' });
  });

  it('avisa quando a unidade foi alterada por outra pessoa', () => {
    stubUnitRejected(
      'POST',
      `/api/units/${FIRST_UNIT.id}/deactivate`,
      409,
      'concurrency.conflict',
      'deactivate',
    );
    cy.visit(UNITS_PATH);

    cy.get('button[aria-label="Desativar Bloco A · 101"]').click();

    cy.get('[data-testid="action-error"]').should(
      'contain.text',
      'Esta unidade foi alterada em outro lugar. A lista já foi atualizada',
    );
  });

  it('troca o metodo de rateio com a versao do resumo', () => {
    cy.intercept('PUT', '/api/units/apportionment-method', {
      statusCode: 200,
      body: { method: 'equalShare', version: '13' },
    }).as('changeMethod');
    cy.visit(UNITS_PATH);

    cy.get('input[type="radio"][value="equalShare"]').check({ force: true });

    cy.wait('@changeMethod').its('request.body').should('deep.equal', {
      version: '12',
      method: 'equalShare',
    });
    cy.get('input[type="radio"][value="equalShare"]').should('be.checked');
  });

  it('mostra o convite para cadastrar quando nao ha unidades', () => {
    stubUnits([], stubSummary({ active: 0, deactivated: 0, apartments: 0 }));
    cy.visit(UNITS_PATH);

    cy.get('[data-testid="empty"]').should('contain.text', 'Nenhuma unidade cadastrada ainda');
  });

  it('leva ao cadastro em lote', () => {
    cy.visit(UNITS_PATH);

    cy.get('[data-testid="batch-link"]').click();

    cy.location('pathname').should('eq', BATCH_PATH);
    cy.contains('h1', 'Cadastro em lote').should('be.visible');
  });

  it('cabe numa tela de 360 px, com o cabecalho inteiro e sem rolagem horizontal da pagina', () => {
    cy.viewport(360, 640);
    cy.visit(UNITS_PATH);

    rows().should('have.length', 2);
    cy.get('[data-testid="user-menu"]').should('be.visible');
    cy.document().should((doc) => {
      expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
    });
  });

  it('manda moradores de volta ao painel', () => {
    stubSignedIn({ ...MANAGER, role: 'Resident' });
    cy.visit(UNITS_PATH);

    cy.location('pathname').should('eq', '/dashboard');
  });
});

describe('Cadastro em lote', () => {
  beforeEach(() => {
    stubSignedIn();
    stubUnits();
  });

  function generator(name: string): Cypress.Chainable<JQuery<HTMLElement>> {
    return cy.get(`[data-testid="generator"] [formControlName="${name}"]`).click('right');
  }

  function buildTwoFloors(): void {
    generator('block').type('{selectall}A');
    generator('firstFloor').type('{selectall}1');
    generator('lastFloor').type('2');
    generator('perFloor').type('2');
    cy.get('[data-testid="build-floors"]').click();
  }

  it('gera os apartamentos por andar, ajusta um andar e cadastra tudo de uma vez', () => {
    cy.intercept('POST', '/api/units/batch', { statusCode: 201, body: { created: 3 } }).as('batch');
    cy.visit(BATCH_PATH);

    buildTwoFloors();
    cy.get('[data-testid="floor-count"][data-floor="2"]').type('{selectall}1');
    cy.get('[data-testid="generate"]').click();

    cy.get('[data-testid="batch-row"]').should('have.length', 3);
    cy.get('[data-testid="row-count"]').should('contain.text', '3 unidades no lote');
    cy.get('[data-testid="batch-row"]').first().find('[data-field="fraction"]').type('2,5');
    cy.get('[data-testid="submit"]').should('contain.text', 'Cadastrar 3 unidades').click();

    cy.wait('@batch')
      .its('request.body')
      .should('deep.equal', {
        units: [
          { block: 'A', number: '101', type: 'apartment', idealFraction: 0.025, area: null },
          { block: 'A', number: '102', type: 'apartment', idealFraction: null, area: null },
          { block: 'A', number: '201', type: 'apartment', idealFraction: null, area: null },
        ],
      });
    cy.get('[data-testid="batch-done"]').should('contain.text', '3 unidades cadastradas');
    cy.get('[data-testid="to-units"]').click();
    cy.location('pathname').should('eq', UNITS_PATH);
  });

  it('marca na tela a linha que a API recusou e nada e cadastrado', () => {
    stubUnitRejected('POST', '/api/units/batch', 409, 'unit.identifier.already_exists', 'batch', {
      errors: [{ position: 1, code: 'unit.identifier.already_exists' }],
    });
    cy.visit(BATCH_PATH);

    buildTwoFloors();
    cy.get('[data-testid="generate"]').click();
    cy.get('[data-testid="submit"]').click();

    cy.wait('@batch');
    cy.get('[data-testid="batch-error"]').should(
      'contain.text',
      'Nenhuma unidade foi cadastrada. Corrija as linhas marcadas e tente de novo',
    );
    cy.get('[data-testid="batch-row"]')
      .eq(1)
      .find('[data-testid="row-error"]')
      .should('contain.text', 'Já existe uma unidade com este bloco e número');
    cy.get('[data-testid="batch-row"]').eq(0).find('[data-testid="row-error"]').should('not.exist');
  });

  it('divide o que falta da fracao ideal entre as linhas', () => {
    cy.visit(BATCH_PATH);

    generator('block').type('B');
    generator('firstFloor').type('{selectall}1');
    generator('lastFloor').type('1');
    generator('perFloor').type('3');
    cy.get('[data-testid="build-floors"]').click();
    cy.get('[data-testid="generate"]').click();
    cy.get('[data-testid="split-fraction"]').click();

    cy.get('[data-testid="batch-row"] [data-field="fraction"]').each(($input) => {
      expect($input.val()).to.equal('32,916666');
    });
  });

  it('mostra os erros do gerador sem cobrir nada', () => {
    cy.visit(BATCH_PATH);

    generator('firstFloor').type('{selectall}{backspace}');
    cy.get('[data-testid="build-floors"]').click();

    cy.get('[data-testid="generator"]').should('contain.text', 'Informe o primeiro andar');
    expectErrorsToStayInTheirOwnSpace();
  });

  it('cabe numa tela de celular, sem rolagem horizontal da pagina', () => {
    cy.viewport(375, 667);
    cy.visit(BATCH_PATH);

    buildTwoFloors();
    cy.get('[data-testid="generate"]').click();

    cy.get('[data-testid="batch-row"]').should('have.length', 4);
    cy.document().then((doc) => {
      expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
    });
  });
});
