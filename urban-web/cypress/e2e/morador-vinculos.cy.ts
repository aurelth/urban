import { chooseMenu } from '../support/menu';
import { DESKTOP, PHONE } from '../support/navegacao';
import { MANAGER, stubSignedIn } from '../support/session';
import { SECOND_UNIT, stubUnits } from '../support/unidades';
import {
  CURRENT_RESIDENT,
  FORMER_RESIDENT,
  MINOR_RESIDENT,
  editResidentPath,
  stubOccupancyAdded,
  stubOccupancyEnded,
  stubOccupancyRejected,
  stubResidentDetail,
  typeInto,
} from '../support/moradores';

const CURRENT_OCCUPANCY = CURRENT_RESIDENT.occupancies[0];

describe('Vínculos do morador', () => {
  beforeEach(() => {
    stubSignedIn(MANAGER);
    stubUnits();
    stubResidentDetail();
    cy.viewport(DESKTOP.width, DESKTOP.height);
  });

  it('mostra as unidades do morador na edição, com o botão de encerrar só no vínculo em andamento', () => {
    cy.visit(editResidentPath(CURRENT_RESIDENT.id));

    cy.get('[data-testid="occupancy"]').should('have.length', 1);
    cy.get('[data-testid="occupancy"]')
      .first()
      .should('contain.text', 'Bloco A · 101')
      .and('contain.text', 'Proprietário, desde 01/03/2024');
    cy.get('[data-testid="end-occupancy"]').should(
      'have.attr',
      'aria-label',
      'Encerrar vínculo em Bloco A · 101',
    );
  });

  it('encerra o vínculo com a data de saída e mostra o aviso', () => {
    stubOccupancyEnded(CURRENT_RESIDENT, CURRENT_OCCUPANCY.id, '2026-11-10');
    cy.visit(editResidentPath(CURRENT_RESIDENT.id));

    cy.get('[data-testid="end-occupancy"]').click();
    typeInto('endDate', '10112026');
    cy.contains('button[type="submit"]', 'Registrar saída').click();

    cy.wait('@endOccupancy').its('request.body').should('deep.equal', { endDate: '2026-11-10' });
    cy.get('[data-testid="occupancy-notice"]').should(
      'contain.text',
      'Saída de Bloco A · 101 registrada',
    );
    cy.get('[data-testid="end-occupancy"]').should('not.exist');
    cy.get('[data-testid="occupancy"]').should('contain.text', 'de 01/03/2024 a 10/11/2026');
  });

  it('avisa da data de saída antes da entrada, sem chamar a API', () => {
    cy.visit(editResidentPath(CURRENT_RESIDENT.id));

    cy.get('[data-testid="end-occupancy"]').click();
    typeInto('endDate', '28022024');
    cy.contains('button[type="submit"]', 'Registrar saída').click();

    cy.contains('mat-error', 'A saída não pode ser antes da entrada').should('be.visible');
  });

  it('mostra o motivo quando a API recusa a saída e mantém a data digitada', () => {
    stubOccupancyRejected(CURRENT_RESIDENT, 'end', 400, 'condominium.occupancy.end_date_invalid');
    cy.visit(editResidentPath(CURRENT_RESIDENT.id));

    cy.get('[data-testid="end-occupancy"]').click();
    typeInto('endDate', '10112026');
    cy.contains('button[type="submit"]', 'Registrar saída').click();

    cy.get('[role="alert"]').should(
      'contain.text',
      'A data de saída deve ficar entre a entrada e hoje',
    );
    cy.get('[formControlName="endDate"]').should('have.value', '10/11/2026');
  });

  it('o menor de idade só ganha outra unidade como dependente', () => {
    stubResidentDetail(MINOR_RESIDENT);
    stubOccupancyAdded(MINOR_RESIDENT, {
      id: '0199cccc-0000-7000-8000-000000000010',
      unitId: SECOND_UNIT.id,
      unitBlock: 'A',
      unitNumber: '102',
      kind: 'dependent',
      startDate: '2026-01-05',
      endDate: null,
    });
    cy.visit(editResidentPath(MINOR_RESIDENT.id));

    cy.get('[data-testid="add-occupancy"]').click();
    chooseMenu('[formControlName="unitId"]', 'Bloco A · 102');
    chooseMenu('[formControlName="kind"]', 'Inquilino');
    typeInto('startDate', '05012026');
    cy.contains('button[type="submit"]', 'Adicionar vínculo').click();
    cy.contains('mat-error', 'Quem tem menos de 18 anos só pode ser dependente').should(
      'be.visible',
    );
    cy.get('@addOccupancy.all').should('have.length', 0);

    chooseMenu('[formControlName="kind"]', 'Dependente');
    cy.contains('button[type="submit"]', 'Adicionar vínculo').click();

    cy.wait('@addOccupancy')
      .its('request.body')
      .should('deep.equal', { unitId: SECOND_UNIT.id, kind: 'dependent', startDate: '2026-01-05' });
    cy.get('[data-testid="occupancy"]').should('contain.text', 'Dependente, desde 01/03/2024');
  });

  it('adiciona o morador a outra unidade', () => {
    stubOccupancyAdded(CURRENT_RESIDENT, {
      id: '0199cccc-0000-7000-8000-000000000009',
      unitId: SECOND_UNIT.id,
      unitBlock: 'A',
      unitNumber: '102',
      kind: 'tenant',
      startDate: '2026-01-05',
      endDate: null,
    });
    cy.visit(editResidentPath(CURRENT_RESIDENT.id));

    cy.get('[data-testid="add-occupancy"]').click();
    chooseMenu('[formControlName="unitId"]', 'Bloco A · 102');
    chooseMenu('[formControlName="kind"]', 'Inquilino');
    typeInto('startDate', '05012026');
    cy.contains('button[type="submit"]', 'Adicionar vínculo').click();

    cy.wait('@addOccupancy')
      .its('request.body')
      .should('deep.equal', { unitId: SECOND_UNIT.id, kind: 'tenant', startDate: '2026-01-05' });
    cy.get('[data-testid="occupancy-notice"]').should(
      'contain.text',
      'Vínculo com Bloco A · 102 adicionado',
    );
    cy.get('[data-testid="occupancy"]').should('have.length', 2);
  });

  it('não oferece a unidade onde o morador já mora', () => {
    cy.visit(editResidentPath(CURRENT_RESIDENT.id));

    cy.get('[data-testid="add-occupancy"]').click();
    cy.get('[formControlName="unitId"]').find('button').click();
    cy.get('[role="menuitemradio"]').should('have.length', 1).and('contain.text', 'Bloco A · 102');
  });

  it('um ex-morador volta a ter o botão de adicionar unidade', () => {
    stubResidentDetail(FORMER_RESIDENT);
    cy.visit(editResidentPath(FORMER_RESIDENT.id));

    cy.get('[data-testid="end-occupancy"]').should('not.exist');
    cy.get('[data-testid="add-occupancy"]').should('be.visible');
  });

  describe('no celular', () => {
    it('os formulários de saída e de nova unidade cabem na tela, sem rolagem horizontal', () => {
      cy.viewport(PHONE.width, PHONE.height);
      cy.visit(editResidentPath(CURRENT_RESIDENT.id));

      cy.get('[data-testid="end-occupancy"]').click();
      cy.get('[data-testid="end-form"]').should('be.visible');
      cy.document().should((doc) => {
        expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
      });

      cy.get('[data-testid="add-occupancy"]').click();
      cy.get('[data-testid="add-form"]').should('be.visible');
      cy.document().should((doc) => {
        expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
      });
    });
  });
});
