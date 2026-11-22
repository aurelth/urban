import {
  ANNOUNCEMENTS_PATH,
  FIRST_ANNOUNCEMENT,
  SECOND_ANNOUNCEMENT,
  stubAnnouncement,
  stubAnnouncementRejected,
  stubAnnouncements,
} from '../support/comunicados';
import { PALETTE, expectErrorsToStayInTheirOwnSpace, themeErrorColor } from '../support/layout';
import { chooseMenu } from '../support/menu';
import { DESKTOP } from '../support/navegacao';
import { MANAGER, stubSignedIn } from '../support/session';
import { SECOND_UNIT, stubUnits } from '../support/unidades';

function rows(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="announcement-row"]');
}

/** O rótulo do Material cobre o meio do campo vazio, então o clique vai para a direita (como nas unidades). */
function enter(name: string, value: string): void {
  cy.get(`urban-announcement-dialog [formControlName="${name}"]`)
    .click('right')
    .type(`{selectall}${value}`);
}

function chooseScope(label: string): void {
  cy.get('urban-announcement-dialog')
    .contains('mat-radio-button', label)
    .find('input')
    .check({ force: true });
}

describe('Comunicados', () => {
  beforeEach(() => {
    stubSignedIn(MANAGER);
    stubUnits();
    stubAnnouncements();
  });

  it('abre pelo menu lateral com a lista, o alcance e quantos já leram', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    cy.visit('/dashboard');
    cy.get('[data-testid="nav-announcements"]').click();

    cy.location('pathname').should('eq', ANNOUNCEMENTS_PATH);
    cy.contains('h1', 'Comunicados').should('be.visible');
    rows().should('have.length', 3);
    rows()
      .eq(0)
      .should('contain.text', 'Reunião de sábado')
      .and('contain.text', 'Importante')
      .and('contain.text', 'Condomínio inteiro')
      .and('contain.text', 'Publicado em 22/11/2026 às 10:50');
    rows().eq(0).find('[data-testid="readers"]').should('have.text', '12 de 40 leram');
    rows()
      .eq(1)
      .should('contain.text', 'Bloco A')
      .and('contain.text', 'Editado em 21/11/2026 às 12:30');
    rows().eq(1).find('[data-testid="readers"]').should('have.text', '1 de 1 leu');
    rows().eq(2).should('contain.text', 'Unidade 102');
    rows()
      .eq(2)
      .find('[data-testid="readers"]')
      .should('have.text', 'Ninguém mora aqui ainda para ler');
  });

  it('publica para um bloco escolhendo o alcance em três passos e avisa em verde', () => {
    cy.intercept('POST', '/api/announcements', { statusCode: 201, body: stubAnnouncement() }).as(
      'create',
    );
    cy.visit(ANNOUNCEMENTS_PATH);

    cy.get('[data-testid="new-announcement"]').click();
    cy.get('urban-announcement-dialog h2').should('contain.text', 'Novo comunicado');
    cy.get('urban-announcement-dialog [formControlName="block"]').should('not.exist');
    enter('title', 'Limpeza da caixa d’água');
    enter('body', 'Sem água das 8h às 12h.');
    chooseScope('Um bloco');
    chooseMenu('urban-announcement-dialog urban-menu-select[formControlName="block"]', 'Bloco A');
    cy.get('urban-announcement-dialog').contains('button', 'Publicar comunicado').click();

    cy.wait('@create').its('request.body').should('deep.equal', {
      title: 'Limpeza da caixa d’água',
      body: 'Sem água das 8h às 12h.',
      category: 'general',
      scope: 'block',
      block: 'A',
      unitId: null,
    });
    cy.get('urban-announcement-dialog').should('not.exist');
    cy.get('@announcements.all').should('have.length.at.least', 2);
    cy.get('[data-testid="notice"]')
      .should('contain.text', 'Comunicado "Limpeza da caixa d’água" publicado')
      .find('.text-success mat-icon')
      .should('have.css', 'color', PALETTE.light.success);
  });

  it('publica para uma unidade', () => {
    cy.intercept('POST', '/api/announcements', { statusCode: 201, body: stubAnnouncement() }).as(
      'create',
    );
    cy.visit(ANNOUNCEMENTS_PATH);

    cy.get('[data-testid="new-announcement"]').click();
    enter('title', 'Encomenda');
    enter('body', 'Retire até sexta.');
    chooseScope('Uma unidade');
    chooseMenu(
      'urban-announcement-dialog urban-menu-select[formControlName="unitId"]',
      'Bloco A · 102',
    );
    cy.get('urban-announcement-dialog').contains('button', 'Publicar comunicado').click();

    cy.wait('@create')
      .its('request.body')
      .should('include', { scope: 'unit', unitId: SECOND_UNIT.id, block: null });
  });

  it('mostra os erros dos campos do diálogo sem cobrir nada', () => {
    cy.visit(ANNOUNCEMENTS_PATH);

    cy.get('[data-testid="new-announcement"]').click();
    chooseScope('Um bloco');
    cy.get('urban-announcement-dialog').contains('button', 'Publicar comunicado').click();

    cy.get('urban-announcement-dialog').should('contain.text', 'Informe o título');
    cy.get('urban-announcement-dialog').should('contain.text', 'Informe o texto');
    cy.get('urban-announcement-dialog').should('contain.text', 'Escolha o bloco');
    expectErrorsToStayInTheirOwnSpace();
  });

  it('mostra no diálogo o motivo da recusa da API', () => {
    stubAnnouncementRejected(
      'POST',
      '/api/announcements',
      422,
      'announcement.block.not_found',
      'create',
    );
    cy.visit(ANNOUNCEMENTS_PATH);

    cy.get('[data-testid="new-announcement"]').click();
    enter('title', 'Aviso');
    enter('body', 'Texto');
    cy.get('urban-announcement-dialog').contains('button', 'Publicar comunicado').click();

    cy.get('urban-announcement-dialog [role="alert"]').should(
      'contain.text',
      'Este bloco não tem unidades cadastradas',
    );
  });

  it('edita um comunicado levando a versão lida', () => {
    cy.intercept('PUT', `/api/announcements/${FIRST_ANNOUNCEMENT.id}`, {
      statusCode: 200,
      body: { ...FIRST_ANNOUNCEMENT, title: 'Reunião de domingo', version: '34' },
    }).as('update');
    cy.visit(ANNOUNCEMENTS_PATH);

    cy.get('button[aria-label="Editar Reunião de sábado"]').click();
    cy.get('urban-announcement-dialog h2').should('contain.text', 'Editar comunicado');
    cy.get('urban-announcement-dialog [formControlName="title"]').should(
      'have.value',
      'Reunião de sábado',
    );
    cy.get('urban-announcement-dialog')
      .contains('button', 'Salvar alterações')
      .should('be.disabled');
    enter('title', 'Reunião de domingo');
    cy.get('urban-announcement-dialog').contains('button', 'Salvar alterações').click();

    cy.wait('@update').its('request.body').should('deep.equal', {
      title: 'Reunião de domingo',
      body: FIRST_ANNOUNCEMENT.body,
      category: 'important',
      scope: 'condominium',
      block: null,
      unitId: null,
      version: '31',
    });
    cy.get('urban-announcement-dialog').should('not.exist');
    cy.get('[data-testid="notice"]').should(
      'contain.text',
      'Comunicado "Reunião de domingo" atualizado',
    );
  });

  it('pede confirmação num diálogo antes de excluir, levando a versão na consulta', () => {
    cy.intercept('DELETE', `/api/announcements/${FIRST_ANNOUNCEMENT.id}?version=31`, {
      statusCode: 204,
    }).as('remove');
    cy.visit(ANNOUNCEMENTS_PATH);

    cy.get('button[aria-label="Excluir Reunião de sábado"]').click();
    cy.get('urban-delete-announcement-dialog')
      .should('contain.text', 'Excluir Reunião de sábado')
      .and('contain.text', 'não se desfaz');
    cy.get('urban-delete-announcement-dialog [data-testid="cancel"]').click();
    cy.get('urban-delete-announcement-dialog').should('not.exist');

    cy.get('button[aria-label="Excluir Reunião de sábado"]').click();
    stubAnnouncements([SECOND_ANNOUNCEMENT], 'afterDelete');
    cy.get('urban-delete-announcement-dialog').contains('button', 'Excluir comunicado').click();

    cy.wait('@remove');
    cy.wait('@afterDelete');
    cy.get('urban-delete-announcement-dialog').should('not.exist');
    rows().should('have.length', 1);
    cy.get('[data-testid="notice"]').should(
      'contain.text',
      'Comunicado "Reunião de sábado" excluído',
    );
  });

  it('os botões de excluir são vermelhos nos dois temas e o do diálogo é só texto', () => {
    cy.visit(ANNOUNCEMENTS_PATH);

    for (const dark of [false, true]) {
      cy.document().then((doc) => doc.documentElement.classList.toggle('app-dark', dark));
      themeErrorColor().then((error) => {
        cy.get('button[aria-label="Excluir Reunião de sábado"]').should('have.css', 'color', error);
      });
    }

    cy.document().then((doc) => doc.documentElement.classList.remove('app-dark'));
    cy.get('button[aria-label="Excluir Reunião de sábado"]').click();
    for (const dark of [false, true]) {
      cy.document().then((doc) => doc.documentElement.classList.toggle('app-dark', dark));
      themeErrorColor().then((error) => {
        cy.get('urban-delete-announcement-dialog button[type="submit"]')
          .should('have.css', 'color', error)
          .and('have.css', 'background-color', 'rgba(0, 0, 0, 0)');
      });
    }
  });

  it('avisa no diálogo quando o comunicado foi alterado em outro lugar', () => {
    stubAnnouncementRejected(
      'PUT',
      `/api/announcements/${FIRST_ANNOUNCEMENT.id}`,
      409,
      'concurrency.conflict',
      'update',
    );
    cy.visit(ANNOUNCEMENTS_PATH);

    cy.get('button[aria-label="Editar Reunião de sábado"]').click();
    enter('title', 'Reunião de domingo');
    cy.get('urban-announcement-dialog').contains('button', 'Salvar alterações').click();

    cy.get('urban-announcement-dialog [role="alert"]').should(
      'contain.text',
      'foi alterado em outro lugar',
    );
  });

  it('sem comunicados, convida a publicar o primeiro', () => {
    stubAnnouncements([]);
    cy.visit(ANNOUNCEMENTS_PATH);

    cy.get('[data-testid="empty"]').should('contain.text', 'Nenhum comunicado publicado ainda');
    cy.get('[data-testid="new-announcement"]').should('be.visible');
  });

  describe('no celular (375 x 667)', () => {
    beforeEach(() => {
      cy.viewport(375, 667);
    });

    it('mostra a lista e o diálogo sem rolagem horizontal', () => {
      cy.visit(ANNOUNCEMENTS_PATH);

      rows().should('have.length', 3);
      cy.document().should((doc) => {
        expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
      });

      cy.get('[data-testid="new-announcement"]').click();
      cy.get('.mat-mdc-dialog-container').should('be.visible');
      cy.get('.mat-mdc-dialog-container').then(($dialog) => {
        expect($dialog[0].getBoundingClientRect().right).to.be.at.most(375);
      });
    });
  });
});
