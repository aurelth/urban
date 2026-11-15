import { DESKTOP, PHONE } from '../support/navegacao';
import { MANAGER, stubSignedIn } from '../support/session';
import { stubUnits } from '../support/unidades';
import { NEW_RESIDENT_PATH, stubResidents, typeInto } from '../support/moradores';

const OPEN = '[data-testid="open-calendar"]';

function openBirthCalendar(): void {
  cy.get('[formControlName="birthDate"]').closest('urban-text-field').find(OPEN).click();
  cy.get('mat-calendar').should('be.visible');
}

function openStartCalendar(): void {
  cy.get('[formControlName="startDate"]').closest('urban-text-field').find(OPEN).click();
  cy.get('mat-calendar').should('be.visible');
}

describe('Calendário dos campos de data', () => {
  beforeEach(() => {
    stubSignedIn(MANAGER);
    stubUnits();
    stubResidents();
  });

  it('data de nascimento: abre na lista de anos e escolhe ano, mês e dia em português', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit(NEW_RESIDENT_PATH);

    openBirthCalendar();
    cy.get('mat-multi-year-view').should('be.visible');
    cy.get('button[aria-label="Anteriores 24 anos"]').click();
    cy.get('.mat-calendar-body-cell[aria-label="1990"]').click();
    cy.get('.mat-calendar-body-cell[aria-label="maio de 1990"]').click();
    cy.get('.mat-calendar-body-cell[aria-label="20 de maio de 1990"]').click();

    cy.get('mat-calendar').should('not.exist');
    cy.get('[formControlName="birthDate"]').should('have.value', '20/05/1990');
    cy.get(`urban-text-field:has([formControlName="birthDate"]) ${OPEN}`).should('be.focused');
  });

  it('data de entrada: abre no mês da data digitada, com o dia marcado, e a digitação continua valendo', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit(NEW_RESIDENT_PATH);

    typeInto('startDate', '01032024');
    cy.get('[formControlName="startDate"]').should('have.value', '01/03/2024');
    openStartCalendar();
    cy.get('.mat-calendar-period-button').should('contain.text', '2024');
    cy.get('.mat-calendar-body-selected').should('have.text', ' 1 ');
    cy.get('.mat-calendar-body-cell[aria-label="20 de março de 2024"]').click();

    cy.get('[formControlName="startDate"]').should('have.value', '20/03/2024');
  });

  it('no computador, abrir o calendário não cria rolagem horizontal', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit(NEW_RESIDENT_PATH);

    openStartCalendar();

    cy.document().should((doc) => {
      expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
    });
  });

  it('Esc fecha o calendário sem mexer na data e devolve o foco ao botão', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit(NEW_RESIDENT_PATH);

    typeInto('startDate', '01032024');
    openStartCalendar();
    // O Material só conclui o fechamento depois da animação de abertura (uns 150 ms); um Esc dentro dela é perdido.
    cy.wait(500);
    cy.get('mat-calendar').find('.mat-calendar-body-active').should('be.focused').type('{esc}');

    cy.get('mat-calendar').should('not.exist');
    cy.get('[formControlName="startDate"]').should('have.value', '01/03/2024');
    cy.get(`urban-text-field:has([formControlName="startDate"]) ${OPEN}`).should('be.focused');
  });

  it('no celular (375 px) o calendário cabe na tela e a página não rola na horizontal', () => {
    cy.viewport(PHONE.width, PHONE.height);
    cy.visit(NEW_RESIDENT_PATH);

    openBirthCalendar();

    cy.get('.mat-datepicker-content').then(($content) => {
      const box = $content[0].getBoundingClientRect();
      expect(box.left).to.be.at.least(0);
      expect(box.right).to.be.at.most(375);
    });
    cy.document().should((doc) => {
      expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
    });
  });
});
