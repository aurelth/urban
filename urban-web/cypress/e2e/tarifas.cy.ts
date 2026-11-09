import { EXPENSES_PATH, TODAY, stubExpenses, stubFinanceRejected } from '../support/financeiro';
import { PALETTE, expectErrorsToStayInTheirOwnSpace } from '../support/layout';
import { chooseMenu } from '../support/menu';
import { stubSignedIn } from '../support/session';
import {
  CURRENT_TARIFF,
  OLD_TARIFF,
  SETTINGS_API,
  TARIFFS_API,
  TARIFFS_PATH,
  bandField,
  stubTariffs,
} from '../support/tarifas';

/** O rótulo do Material cobre o meio do campo vazio, então o clique vai para a direita (como em "Meus dados"). */
function enter(field: Cypress.Chainable<JQuery<HTMLElement>>, value: string): void {
  field.click('right').type(`{selectall}${value}`);
}

/** O Material leva o foco ao primeiro campo só depois de abrir o diálogo; digitar antes disso perde o foco. */
function dialogHasFocus(tag: string): void {
  cy.focused().closest(tag).should('exist');
}

function items(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="tariff-item"]');
}

describe('Tarifas de consumo', () => {
  beforeEach(() => {
    cy.clock(TODAY, ['Date']);
    stubSignedIn();
    stubTariffs();
  });

  it('abre pelo link da tela de despesas com o fundo de reserva e as tarifas', () => {
    stubExpenses();
    cy.visit(EXPENSES_PATH);

    cy.get('[data-testid="tariffs-link"]').click();

    cy.location('pathname').should('eq', TARIFFS_PATH);
    cy.contains('h1', 'Tarifas de consumo').should('be.visible');
    cy.get('[data-testid="reserve-percent"]').should(
      'contain.text',
      '5% das despesas dos grupos marcados como base',
    );
    items().should('have.length', 2);
    items().first().should('contain.text', 'A partir de novembro de 2026');
    items().first().should('contain.text', '3 faixas').and('contain.text', 'R$\u00a037,70 por m³');
    items().last().should('contain.text', 'Esgoto').and('contain.text', 'Não cobrado');
  });

  it('marca em verde, com texto e ícone, a tarifa em vigor', () => {
    cy.visit(TARIFFS_PATH);

    cy.get('[data-testid="in-force-badge"]')
      .should('have.length', 1)
      .and('contain.text', 'Em vigor');
    cy.get('[data-testid="in-force-badge"]')
      .find('mat-icon')
      .should('have.css', 'color', PALETTE.light.success);
    items().first().find('[data-testid="in-force-badge"]').should('exist');
  });

  it('cadastra uma tarifa com duas faixas de água, esgoto e gás', () => {
    cy.intercept('PUT', `${TARIFFS_API}/2026-12`, { statusCode: 200, body: CURRENT_TARIFF }).as(
      'saveTariff',
    );
    cy.visit(TARIFFS_PATH);

    cy.get('[data-testid="new-tariff"]').click();
    dialogHasFocus('urban-tariff-dialog');
    cy.get('urban-tariff-dialog h2').should('contain.text', 'Nova tarifa de consumo');
    chooseMenu('urban-tariff-dialog [formControlName="month"]', 'Dezembro de 2026');

    cy.get('urban-tariff-dialog [data-testid="water-bands"] [data-testid="add-band"]').click();
    enter(bandField('water', 0, 'upTo'), '10');
    enter(bandField('water', 0, 'fixed'), '28,41');
    enter(bandField('water', 0, 'price'), '0');
    enter(bandField('water', 1, 'price'), '5,1');

    cy.get('urban-tariff-dialog [data-testid="sewage-enabled"]').click();
    cy.get('urban-tariff-dialog [data-testid="copy-water-bands"]').click();
    enter(cy.get('urban-tariff-dialog [formControlName="sewagePercent"]'), '50');

    cy.get('urban-tariff-dialog [data-testid="gas-enabled"]').click();
    enter(cy.get('urban-tariff-dialog [formControlName="kilos"]'), '2,4');
    enter(cy.get('urban-tariff-dialog [formControlName="pricePerKilo"]'), '15,71');
    cy.get('urban-tariff-dialog [data-testid="gas-price"]').should('contain.text', 'R$\u00a037,70');

    cy.get('urban-tariff-dialog').contains('button', 'Salvar tarifa').click();

    cy.wait('@saveTariff')
      .its('request.body')
      .should('deep.equal', {
        water: [
          { upToCubicMeters: 10, fixedAmount: 28.41, pricePerCubicMeter: 0 },
          { upToCubicMeters: null, fixedAmount: 0, pricePerCubicMeter: 5.1 },
        ],
        sewage: [
          { upToCubicMeters: 10, fixedAmount: 28.41, pricePerCubicMeter: 0 },
          { upToCubicMeters: null, fixedAmount: 0, pricePerCubicMeter: 5.1 },
        ],
        sewagePercent: 50,
        gas: { kilosPerCubicMeter: 2.4, pricePerKilo: 15.71 },
      });
    cy.get('urban-tariff-dialog').should('not.exist');
    cy.get('[data-testid="notice"]')
      .should('contain.text', 'Tarifa salva')
      .find('.text-success mat-icon')
      .should('have.css', 'color', PALETTE.light.success);
  });

  it('explica o que falta e avisa de limites que não crescem, sem cobrir outros campos', () => {
    stubTariffs([OLD_TARIFF]);
    cy.visit(TARIFFS_PATH);

    cy.get('[data-testid="new-tariff"]').click();
    dialogHasFocus('urban-tariff-dialog');
    cy.get('urban-tariff-dialog [data-testid="water-bands"] [data-testid="add-band"]').click();
    enter(bandField('water', 0, 'upTo'), '20');
    cy.get('urban-tariff-dialog [data-testid="water-bands"] [data-testid="add-band"]').click();
    enter(bandField('water', 1, 'upTo'), '10');
    cy.get('urban-tariff-dialog').contains('button', 'Salvar tarifa').click();

    cy.get('urban-tariff-dialog').should(
      'contain.text',
      'Informe o preço por m³, com até 4 casas (use 0 se não houver)',
    );
    cy.get('[data-testid="water-ascending-error"]').should(
      'contain.text',
      'Cada limite deve ser maior que o da faixa anterior',
    );
    expectErrorsToStayInTheirOwnSpace();
  });

  it('recusa um mês que já tem tarifa', () => {
    cy.visit(TARIFFS_PATH);

    cy.get('[data-testid="new-tariff"]').click();
    dialogHasFocus('urban-tariff-dialog');
    cy.get('urban-tariff-dialog [data-testid="water-bands"]').should('exist');
    enter(bandField('water', 0, 'price'), '5');
    cy.get('urban-tariff-dialog').contains('button', 'Salvar tarifa').click();

    cy.get('urban-tariff-dialog').should(
      'contain.text',
      'Já existe uma tarifa que começa neste mês. Edite a tarifa dela na lista',
    );
  });

  it('edita a tarifa no mês dela, com os valores atuais já preenchidos', () => {
    cy.intercept('PUT', `${TARIFFS_API}/2026-03`, { statusCode: 200, body: OLD_TARIFF }).as(
      'saveTariff',
    );
    cy.visit(TARIFFS_PATH);

    cy.get('button[aria-label="Editar tarifa de março de 2026"]').click();
    dialogHasFocus('urban-tariff-dialog');
    cy.get('urban-tariff-dialog h2').should('contain.text', 'Editar tarifa de março de 2026');
    bandField('water', 0, 'fixed').should('have.value', '25,00');
    enter(bandField('water', 0, 'price'), '5,2');
    cy.get('urban-tariff-dialog').contains('button', 'Salvar alterações').click();

    cy.wait('@saveTariff')
      .its('request.body.water')
      .should('deep.equal', [{ upToCubicMeters: null, fixedAmount: 25, pricePerCubicMeter: 5.2 }]);
    cy.get('[data-testid="notice"]').should('contain.text', 'Tarifa salva');
  });

  it('mostra o erro da API dentro do diálogo, sem fechá-lo', () => {
    stubFinanceRejected('PUT', `${TARIFFS_API}/2026-11`, 400, 'consumption.bands_invalid', 'bad');
    cy.visit(TARIFFS_PATH);
    cy.get('button[aria-label="Editar tarifa de novembro de 2026"]').click();
    dialogHasFocus('urban-tariff-dialog');

    cy.get('urban-tariff-dialog').contains('button', 'Salvar alterações').click();

    cy.wait('@bad');
    cy.get('urban-tariff-dialog [role="alert"]').should(
      'contain.text',
      'As faixas precisam ser de 1 a 6, com limites crescentes, a última sem limite',
    );
  });

  it('apaga uma tarifa depois de confirmar', () => {
    cy.intercept('DELETE', `${TARIFFS_API}/2026-03`, { statusCode: 204 }).as('deleteTariff');
    cy.visit(TARIFFS_PATH);

    cy.get('button[aria-label="Apagar tarifa de março de 2026"]').click();
    cy.get('urban-delete-tariff-dialog').should('contain.text', 'Os meses já fechados não mudam');
    cy.get('urban-delete-tariff-dialog').contains('button', 'Apagar tarifa').click();

    cy.wait('@deleteTariff');
    cy.get('[data-testid="notice"]').should('contain.text', 'Tarifa apagada');
  });

  it('muda o percentual do fundo de reserva', () => {
    cy.intercept('PUT', SETTINGS_API, { statusCode: 200, body: { reserveFundPercent: 2.5 } }).as(
      'savePercent',
    );
    cy.visit(TARIFFS_PATH);

    cy.get('[data-testid="edit-reserve-percent"]').click();
    dialogHasFocus('urban-reserve-percent-dialog');
    cy.get('urban-reserve-percent-dialog [formControlName="percent"]').should('have.value', '5');
    enter(cy.get('urban-reserve-percent-dialog [formControlName="percent"]'), '2,5');
    cy.get('urban-reserve-percent-dialog').contains('button', 'Salvar alterações').click();

    cy.wait('@savePercent').its('request.body').should('deep.equal', { reserveFundPercent: 2.5 });
    cy.get('[data-testid="notice"]').should('contain.text', 'Percentual do fundo de reserva');
  });

  it('cabe numa tela de 360 px, com o diálogo de tarifa inteiro e sem rolagem horizontal', () => {
    cy.viewport(360, 640);
    cy.visit(TARIFFS_PATH);

    items().should('have.length', 2);
    cy.document().should((doc) => {
      expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
    });

    cy.get('button[aria-label="Editar tarifa de novembro de 2026"]').click();
    dialogHasFocus('urban-tariff-dialog');
    cy.get('urban-tariff-dialog [data-testid="band-row"]').should('have.length.greaterThan', 2);
    cy.get('urban-tariff-dialog').then(($dialog) => {
      const { left, right } = $dialog[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(360);
    });
    cy.get('urban-tariff-dialog mat-form-field').each(($field) => {
      const { left, right } = $field[0].getBoundingClientRect();
      expect(left).to.be.at.least(0);
      expect(right).to.be.at.most(360);
    });
  });
});
