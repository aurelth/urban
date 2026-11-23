import {
  ANNOUNCEMENTS_PATH,
  FIRST_ANNOUNCEMENT,
  SECOND_ANNOUNCEMENT,
  THIRD_ANNOUNCEMENT,
  stubAnnouncement,
  stubAnnouncementRejected,
  stubAnnouncements,
} from '../support/comunicados';
import { PALETTE, expectErrorsToStayInTheirOwnSpace, themeErrorColor } from '../support/layout';
import { chooseMenu } from '../support/menu';
import { DESKTOP } from '../support/navegacao';
import { MANAGER, stubSignedIn } from '../support/session';
import { SECOND_UNIT, stubUnits } from '../support/unidades';

/** Texto do elemento sem os espaços das pontas: o ícone e a quebra de linha do template ficam fora da conta. */
function textOf(expected: string): (element: JQuery<HTMLElement>) => void {
  return (element) => {
    expect(element.text().replace(/\s+/g, ' ').trim()).to.eq(expected);
  };
}

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
    rows().eq(0).find('[data-testid="readers"]').should(textOf('12 de 40 leram'));
    rows().eq(0).find('[data-testid="emailed"]').should(textOf('Avisado por e-mail'));
    rows().eq(1).find('[data-testid="emailed"]').should('not.exist');
    rows()
      .eq(1)
      .should('contain.text', 'Bloco A')
      .and('contain.text', 'Editado em 21/11/2026 às 12:30');
    rows().eq(1).find('[data-testid="readers"]').should(textOf('1 de 1 leu'));
    rows().eq(2).should('contain.text', 'Unidade 102');
    rows().eq(2).find('[data-testid="readers"]').should(textOf('Ninguém mora aqui ainda para ler'));
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
      notifyByEmail: false,
    });
    cy.get('urban-announcement-dialog').should('not.exist');
    cy.get('@announcements.all').should('have.length.at.least', 2);
    cy.get('[data-testid="notice"]')
      .should('contain.text', 'Comunicado "Limpeza da caixa d’água" publicado')
      .find('.text-success mat-icon')
      .should('have.css', 'color', PALETTE.light.success);
  });

  it('já marca o aviso por e-mail num comunicado importante e deixa o síndico desmarcar', () => {
    cy.intercept('POST', '/api/announcements', { statusCode: 201, body: stubAnnouncement() }).as(
      'create',
    );
    cy.visit(ANNOUNCEMENTS_PATH);

    cy.get('[data-testid="new-announcement"]').click();
    cy.get('urban-announcement-dialog [data-testid="notify-email"] input').should('not.be.checked');
    chooseMenu(
      'urban-announcement-dialog urban-menu-select[formControlName="category"]',
      'Importante',
    );
    cy.get('urban-announcement-dialog [data-testid="notify-email"] input').should('be.checked');
    enter('title', 'Falta de água');
    enter('body', 'Volta às 18h.');
    cy.get('urban-announcement-dialog').contains('button', 'Publicar comunicado').click();

    cy.wait('@create').its('request.body').should('include', {
      category: 'important',
      notifyByEmail: true,
    });

    cy.get('[data-testid="new-announcement"]').click();
    chooseMenu(
      'urban-announcement-dialog urban-menu-select[formControlName="category"]',
      'Importante',
    );
    cy.get('urban-announcement-dialog [data-testid="notify-email"] input').uncheck({ force: true });
    enter('title', 'Outra falta');
    enter('body', 'Sem e-mail dessa vez.');
    cy.get('urban-announcement-dialog').contains('button', 'Publicar comunicado').click();

    cy.wait('@create').its('request.body').should('include', { notifyByEmail: false });
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

  it('mantém o título na cor de sempre e pinta cada grau, público e a leitura com a sua cor, nos dois temas', () => {
    cy.visit(ANNOUNCEMENTS_PATH);
    rows().should('have.length', 3);

    const colors = {
      light: {
        primary: 'rgb(88, 86, 214)',
        category: ['rgb(194, 0, 26)', 'rgb(138, 90, 0)', 'rgb(11, 92, 173)'],
        scope: ['rgb(13, 107, 99)', 'rgb(126, 34, 206)', 'rgb(190, 24, 93)'],
        readers: 'rgb(26, 113, 49)',
      },
      dark: {
        primary: 'rgb(158, 156, 255)',
        category: ['rgb(255, 105, 97)', 'rgb(255, 214, 10)', 'rgb(100, 181, 255)'],
        scope: ['rgb(94, 234, 212)', 'rgb(216, 180, 254)', 'rgb(249, 168, 212)'],
        readers: 'rgb(48, 209, 88)',
      },
    };
    rows().eq(0).find('[data-testid="title"]').should('have.css', 'color', PALETTE.light.onSurface);
    for (const theme of ['light', 'dark'] as const) {
      cy.document().then((doc) =>
        doc.documentElement.classList.toggle('app-dark', theme === 'dark'),
      );
      const expected = colors[theme];
      for (let index = 0; index < 3; index += 1) {
        rows()
          .eq(index)
          .find('[data-testid="title"]')
          .should('not.have.css', 'color', expected.primary);
        rows()
          .eq(index)
          .find('[data-testid="category"]')
          .should('have.css', 'color', expected.category[index]);
        rows()
          .eq(index)
          .find('[data-testid="scope"]')
          .should('have.css', 'color', expected.scope[index]);
      }
      rows().eq(0).find('[data-testid="readers"]').should('have.css', 'color', expected.readers);
      rows().eq(1).find('[data-testid="readers"]').should('have.css', 'color', expected.readers);
      rows()
        .eq(2)
        .find('[data-testid="readers"]')
        .should('not.have.css', 'color', expected.readers);
    }
    cy.document().then((doc) => doc.documentElement.classList.remove('app-dark'));
  });

  it('o olho de quantos leram cabe na etiqueta, com 18 px', () => {
    cy.visit(ANNOUNCEMENTS_PATH);

    cy.get('[data-testid="readers"] mat-icon').first().should('have.css', 'width', '18px');
    cy.get('[data-testid="readers"] mat-icon').first().should('have.css', 'height', '18px');
  });

  describe('texto longo', () => {
    const LONG_BODY = Array.from({ length: 12 }, (_, line) => `Linha ${line + 1} do aviso`).join(
      '\n',
    );

    beforeEach(() => {
      stubAnnouncements([
        stubAnnouncement({ body: LONG_BODY }),
        SECOND_ANNOUNCEMENT,
        THIRD_ANNOUNCEMENT,
      ]);
    });

    it('corta em 4 linhas com "Ver mais" em índigo, abre tudo e volta com "Ver menos"', () => {
      cy.visit(ANNOUNCEMENTS_PATH);
      const body = (): Cypress.Chainable<JQuery<HTMLElement>> =>
        rows().eq(0).find('[data-testid="body"]');

      body().should('have.css', 'overflow', 'hidden');
      body().then(($body) => {
        const lineHeight = parseFloat(getComputedStyle($body[0]).lineHeight);
        expect($body[0].clientHeight).to.be.closeTo(lineHeight * 4, 2);
      });
      rows()
        .eq(0)
        .find('[data-testid="toggle-body"]')
        .should(textOf('Ver mais'))
        .and('have.attr', 'aria-expanded', 'false')
        .and('have.css', 'color', PALETTE.light.primary);

      rows().eq(0).find('[data-testid="toggle-body"]').click();
      body().then(($body) => {
        expect($body[0].scrollHeight).to.be.at.most($body[0].clientHeight + 1);
      });
      body().should('contain.text', 'Linha 12 do aviso');
      rows()
        .eq(0)
        .find('[data-testid="toggle-body"]')
        .should(textOf('Ver menos'))
        .and('have.attr', 'aria-expanded', 'true')
        .and('have.css', 'color', PALETTE.light.primary);

      rows().eq(0).find('[data-testid="toggle-body"]').click();
      body().should('have.css', 'overflow', 'hidden');
      rows().eq(0).find('[data-testid="toggle-body"]').should(textOf('Ver mais'));
    });

    it('só oferece "Ver mais" nos textos que passam de 4 linhas', () => {
      cy.visit(ANNOUNCEMENTS_PATH);

      rows().eq(0).find('[data-testid="toggle-body"]').should('exist');
      rows().eq(1).find('[data-testid="toggle-body"]').should('not.exist');
      rows().eq(2).find('[data-testid="toggle-body"]').should('not.exist');
    });

    it('o botão é índigo claro no tema escuro', () => {
      cy.visit(ANNOUNCEMENTS_PATH);

      cy.document().then((doc) => doc.documentElement.classList.add('app-dark'));
      rows()
        .eq(0)
        .find('[data-testid="toggle-body"]')
        .should('have.css', 'color', 'rgb(158, 156, 255)');
      cy.document().then((doc) => doc.documentElement.classList.remove('app-dark'));
    });
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
