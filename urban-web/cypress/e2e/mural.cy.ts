import { TODAY } from '../support/financeiro';
import { DESKTOP } from '../support/navegacao';
import { stubMyAccess } from '../support/minha-fatura';
import { stubDashboard } from '../support/painel';
import {
  MEETING,
  MURAL_ITEMS,
  MURAL_PATH,
  PARCEL,
  POOL,
  WATER,
  oldNotices,
  stubMural,
  stubMuralFailingOnce,
} from '../support/mural';
import { MANAGER, RESIDENT, stubSignedIn } from '../support/session';

/** O texto sem os espaços das pontas, que o Angular mantém entre a marcação e o texto. */
function textIs(expected: string): (element: JQuery<HTMLElement>) => void {
  return (element) => {
    expect(element.text().trim()).to.eq(expected);
  };
}

function card(title: string): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.contains('[data-testid="mural-card"]', title);
}

describe('Mural do morador', () => {
  beforeEach(() => {
    cy.clock(TODAY, ['Date']);
    stubSignedIn(RESIDENT);
  });

  it('é o painel do morador: o mural no lugar do cartão de boas-vindas', () => {
    stubMural();
    cy.visit('/dashboard');

    cy.contains('h1', 'Mural').should('be.visible');
    cy.get('[data-testid="welcome"]').should('not.exist');
    cy.get('[data-testid="my-invoice-link"]').should('not.exist');
    cy.get('[data-testid="mural-card"]').should('have.length', MURAL_ITEMS.length);
  });

  it('resume quantos avisos são novos e quantos há em cada categoria', () => {
    stubMural();
    cy.visit('/dashboard');

    cy.get('[data-testid="summary"]').should('contain.text', 'Você tem 3 avisos novos');
    cy.get('[data-testid="chip-all"]').should('contain.text', 'Todos').and('contain.text', '3');
    cy.get('[data-testid="chip-important"]').should('contain.text', '1');
    cy.get('[data-testid="chip-maintenance"]').should('contain.text', '1');
    cy.get('[data-testid="chip-general"]').should('contain.text', '1');
    cy.get('[data-testid="shown-count"]').should(textIs('Mostrando 4 de 4 avisos'));
  });

  it('mostra em cada cartão a categoria, para quem vale e quando saiu', () => {
    stubMural();
    cy.visit('/dashboard');

    card(MEETING.title).within(() => {
      cy.get('[data-testid="category"]').should(textIs('Importante'));
      cy.get('[data-testid="scope"]').should(textIs('Condomínio inteiro'));
      cy.get('[data-testid="published"]')
        .should(textIs('há 1 h'))
        .and('have.attr', 'title', '15/11/2026 às 11:00');
      cy.get('[data-testid="new-badge"]').should(textIs('Novo'));
    });
    card(WATER.title).within(() => {
      cy.get('[data-testid="scope"]').should(textIs('Bloco A'));
      cy.get('[data-testid="published"]').should(textIs('ontem'));
      cy.get('[data-testid="edited"]').should(textIs('Editado'));
    });
    card(PARCEL.title).within(() => {
      cy.get('[data-testid="scope"]').should(textIs('Sua unidade 101 do bloco A'));
      cy.get('[data-testid="published"]').should(textIs('há 3 dias'));
    });
    card(POOL.title).within(() => {
      cy.get('[data-testid="published"]').should(textIs('01/11/2026'));
      cy.get('[data-testid="new-badge"]').should('not.exist');
      cy.get('[data-testid="read-state"]').should('contain.text', 'Lido em 02/11/2026');
    });
  });

  it('guarda as quebras de linha do aviso', () => {
    stubMural();
    cy.visit('/dashboard');

    card(MEETING.title)
      .find('[data-testid="body"]')
      .should('have.css', 'white-space', 'pre-line')
      .and('contain.text', 'Traga a sua opinião.');
  });

  describe('filtros', () => {
    it('mostra só uma categoria e volta a todas', () => {
      stubMural();
      cy.visit('/dashboard');
      cy.get('[data-testid="mural-card"]').should('have.length', 4);

      cy.get('[data-testid="chip-maintenance"]').click();

      cy.get('[data-testid="chip-maintenance"]').should('have.attr', 'aria-pressed', 'true');
      cy.get('[data-testid="mural-card"]').should('have.length', 1);
      card(WATER.title).should('be.visible');

      cy.get('[data-testid="chip-all"]').click();

      cy.get('[data-testid="mural-card"]').should('have.length', 4);
      cy.get('[data-testid="chip-all"]').should('have.attr', 'aria-pressed', 'true');
    });

    it('"Só não lidos" esconde o que já foi lido', () => {
      stubMural();
      cy.visit('/dashboard');

      cy.get('[data-testid="unread-only"]').click();

      cy.get('[data-testid="unread-only"]').should('have.attr', 'aria-pressed', 'true');
      cy.get('[data-testid="mural-card"]').should('have.length', 3);
      cy.contains('[data-testid="mural-card"]', POOL.title).should('not.exist');
    });

    it('as duas escolhas valem juntas, e sem resultado o mural explica e deixa ver tudo', () => {
      stubMural();
      cy.visit('/dashboard');

      cy.get('[data-testid="chip-general"]').click();
      cy.get('[data-testid="unread-only"]').click();
      cy.get('[data-testid="mural-card"]').should('have.length', 1);
      card(PARCEL.title).find('[data-testid="mark-read"]').click();
      cy.wait('@markRead');
      cy.get('[data-testid="refresh"]').click();

      cy.get('[data-testid="empty-filter"]').should('contain.text', 'Nenhum aviso neste filtro');
      cy.get('[data-testid="clear-filters"]').click();

      cy.get('[data-testid="empty-filter"]').should('not.exist');
      cy.get('[data-testid="mural-card"]').should('have.length', 4);
      cy.get('[data-testid="chip-all"]').should('have.attr', 'aria-pressed', 'true');
      cy.get('[data-testid="unread-only"]').should('have.attr', 'aria-pressed', 'false');
    });
  });

  describe('marcar como lido', () => {
    it('muda o cartão e o resumo na hora', () => {
      stubMural();
      cy.visit('/dashboard');

      card(MEETING.title).find('[data-testid="mark-read"]').click();

      cy.wait('@markRead').its('request.url').should('contain', `/${MEETING.id}/read`);
      card(MEETING.title).within(() => {
        cy.get('[data-testid="new-badge"]').should('not.exist');
        cy.get('[data-testid="mark-read"]').should('not.exist');
        cy.get('[data-testid="read-state"]').should('contain.text', 'Lido em 15/11/2026 às 12:00');
      });
      cy.get('[data-testid="summary"]').should('contain.text', 'Você tem 2 avisos novos');
      cy.get('[data-testid="chip-important"]').should('not.contain.text', '1');
    });

    it('desfaz e avisa quando a API recusa', () => {
      stubMural();
      cy.intercept('POST', /\/api\/announcements\/[0-9a-f-]+\/read$/, {
        statusCode: 500,
        body: { status: 500, code: 'erro', detail: 'Erro' },
      }).as('markReadFailed');
      cy.visit('/dashboard');

      card(MEETING.title).find('[data-testid="mark-read"]').click();

      cy.wait('@markReadFailed');
      cy.get('[data-testid="action-error"]')
        .should('have.attr', 'role', 'alert')
        .and('contain.text', 'Não foi possível marcar o aviso como lido');
      card(MEETING.title).find('[data-testid="new-badge"]').should('exist');
      cy.get('[data-testid="summary"]').should('contain.text', 'Você tem 3 avisos novos');
    });
  });

  describe('marcar todos como lidos', () => {
    it('marca o mural inteiro e avisa quantos foram', () => {
      stubMural();
      cy.visit('/dashboard');

      cy.get('[data-testid="mark-all"]').should(textIs('Marcar todos como lidos')).click();

      cy.wait('@markAll');
      cy.get('[data-testid="notice"]').should('contain.text', '3 avisos marcados como lidos');
      cy.get('[data-testid="summary"]').should('contain.text', 'Você está em dia');
      cy.get('[data-testid="new-badge"]').should('not.exist');
      cy.get('[data-testid="mark-all"]').should('be.disabled');
    });

    it('com uma categoria escolhida, marca só os avisos dela', () => {
      stubMural();
      cy.visit('/dashboard');

      cy.get('[data-testid="chip-general"]').click();
      cy.get('[data-testid="mark-all"]').should(textIs('Marcar avisos gerais como lidos')).click();

      cy.wait('@markAll').its('request.url').should('contain', 'category=general');
      cy.get('[data-testid="notice"]').should('contain.text', '1 aviso marcado como lido');
      cy.get('[data-testid="chip-all"]').click();
      cy.get('[data-testid="summary"]').should('contain.text', 'Você tem 2 avisos novos');
    });
  });

  describe('lista longa', () => {
    it('mostra 10 por vez e acrescenta o resto em "Mostrar mais avisos"', () => {
      stubMural([...MURAL_ITEMS, ...oldNotices(8)]);
      cy.visit('/dashboard');

      cy.get('[data-testid="mural-card"]').should('have.length', 10);
      cy.get('[data-testid="shown-count"]').should(textIs('Mostrando 10 de 12 avisos'));

      cy.get('[data-testid="load-more"]').click();

      cy.get('[data-testid="mural-card"]').should('have.length', 12);
      cy.get('[data-testid="shown-count"]').should(textIs('Mostrando 12 de 12 avisos'));
      cy.get('[data-testid="load-more"]').should('not.exist');
    });
  });

  describe('sem avisos e com erro', () => {
    it('diz que não há aviso por enquanto', () => {
      stubMural([]);
      cy.visit('/dashboard');

      cy.get('[data-testid="empty"]').should('contain.text', 'Nenhum aviso por enquanto');
      cy.get('[data-testid="summary"]').should('contain.text', 'Você está em dia');
    });

    it('mostra o erro e busca de novo em "Tentar de novo"', () => {
      stubMuralFailingOnce();
      cy.visit('/dashboard');

      cy.get('[data-testid="load-error"]').should(
        'contain.text',
        'Não foi possível carregar os avisos',
      );
      cy.get('[data-testid="retry"]').click();

      cy.get('[data-testid="load-error"]').should('not.exist');
      cy.get('[data-testid="mural-card"]').should('have.length', MURAL_ITEMS.length);
    });
  });

  it('"Atualizar avisos" traz o que chegou depois de a tela abrir', () => {
    stubMural();
    cy.visit('/dashboard');
    cy.get('[data-testid="mural-card"]').should('have.length', 4);

    stubMural([
      ...MURAL_ITEMS,
      { ...PARCEL, id: '0199cccc-0000-7000-8000-000000000009', title: 'Aviso novíssimo' },
    ]);
    cy.get('[data-testid="refresh"]').click();

    cy.contains('[data-testid="mural-card"]', 'Aviso novíssimo').should('be.visible');
  });
});

describe('Mural do síndico que mora no condomínio', () => {
  beforeEach(() => {
    cy.clock(TODAY, ['Date']);
    cy.viewport(DESKTOP.width, DESKTOP.height);
    stubSignedIn(MANAGER);
  });

  it('abre pelo item "Mural" do menu, logo depois do Painel', () => {
    stubMyAccess('unit');
    stubDashboard();
    stubMural();
    cy.visit('/dashboard');

    cy.get('nav[aria-label="Menu principal"] a').should(($links) => {
      expect([...$links].slice(0, 3).map((link) => link.dataset['testid'])).to.deep.eq([
        'nav-dashboard',
        'nav-mural',
        'nav-my-invoice',
      ]);
    });
    cy.get('[data-testid="nav-mural"]').should('contain.text', 'Mural').click();

    cy.location('pathname').should('eq', MURAL_PATH);
    cy.contains('h1', 'Mural').should('be.visible');
    cy.get('[data-testid="mural-card"]').should('have.length', MURAL_ITEMS.length);
    cy.get('[data-testid="nav-mural"]').should('have.attr', 'aria-current', 'page');
  });

  it('o painel do síndico continua sendo o de gestão, sem o mural', () => {
    stubMyAccess('unit');
    stubDashboard();
    stubMural();
    cy.visit('/dashboard');

    cy.get('[data-testid="card-month"]').should('exist');
    cy.get('[data-testid="mural-card"]').should('not.exist');
  });
});
