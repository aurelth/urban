import { TODAY } from '../support/financeiro';
import { hubConnected, pushAnnouncementChange, visitWithFakeHub } from '../support/hub';
import { MURAL_ITEMS, stubMural, stubMuralItem } from '../support/mural';
import { DESKTOP, PHONE } from '../support/navegacao';
import { MANAGER, RESIDENT, stubSignedIn } from '../support/session';
import { stubMyAccess } from '../support/minha-fatura';

const FRESH = stubMuralItem({
  id: '0199cccc-0000-7000-8000-000000000099',
  title: 'Aviso que acabou de chegar',
  category: 'important',
  createdAt: '2026-11-15T14:59:00Z',
});

function expectNoHorizontalScroll(): void {
  cy.document().should((doc) => {
    expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
  });
}

describe('Mural ao vivo', () => {
  beforeEach(() => {
    cy.clock(TODAY, ['Date']);
  });

  describe('morador, no computador', () => {
    beforeEach(() => {
      cy.viewport(DESKTOP.width, DESKTOP.height);
      stubSignedIn(RESIDENT);
    });

    it('mostra a bolinha no Painel, que é o mural dele, assim que entra', () => {
      stubMural();
      visitWithFakeHub('/dashboard');

      cy.get('[data-testid="nav-dashboard"] [data-testid="nav-badge"]').should('contain.text', '3');
    });

    it('avisa que há avisos novos sem mexer na lista, e Atualizar traz o aviso', () => {
      const api = stubMural();
      visitWithFakeHub('/dashboard');
      cy.get('[data-testid="mural-card"]').should('have.length', MURAL_ITEMS.length);
      hubConnected();
      cy.get('[data-testid="live-banner"]').should('not.exist');

      cy.then(() => api.publish(FRESH));
      pushAnnouncementChange('published', FRESH.id);

      cy.get('[data-testid="live-banner"]')
        .should('contain.text', 'Há avisos novos')
        .and('have.attr', 'role', 'status');
      cy.get('[data-testid="summary"]').should('contain.text', 'Você tem 4 avisos novos');
      cy.get('[data-testid="chip-all"]').should('contain.text', '4');
      cy.get('[data-testid="chip-important"]').should('contain.text', '2');
      cy.get('[data-testid="nav-dashboard"] [data-testid="nav-badge"]').should('contain.text', '4');
      cy.get('[data-testid="mural-card"]').should('have.length', MURAL_ITEMS.length);
      cy.contains('[data-testid="mural-card"]', FRESH.title).should('not.exist');

      cy.get('[data-testid="live-refresh"]').click();

      cy.get('[data-testid="mural-card"]').should('have.length', MURAL_ITEMS.length + 1);
      cy.contains('[data-testid="mural-card"]', FRESH.title).should('be.visible');
      cy.get('[data-testid="live-banner"]').should('not.exist');
    });

    it('a bolinha do menu diminui quando o morador lê um aviso', () => {
      stubMural();
      visitWithFakeHub('/dashboard');
      cy.get('[data-testid="nav-dashboard"] [data-testid="nav-badge"]').should('contain.text', '3');

      cy.get('[data-testid="mark-read"]').first().click();

      cy.get('[data-testid="nav-dashboard"] [data-testid="nav-badge"]').should('contain.text', '2');
    });

    it('a bolinha some quando o morador lê tudo', () => {
      stubMural();
      visitWithFakeHub('/dashboard');
      cy.get('[data-testid="nav-dashboard"] [data-testid="nav-badge"]').should('exist');

      cy.get('[data-testid="mark-all"]').click();

      cy.get('[data-testid="nav-dashboard"] [data-testid="nav-badge"]').should('not.exist');
    });

    it('um aviso apagado ou editado pede para atualizar, com outro texto', () => {
      stubMural();
      visitWithFakeHub('/dashboard');
      cy.get('[data-testid="mural-card"]').should('have.length', MURAL_ITEMS.length);
      hubConnected();

      pushAnnouncementChange('removed', MURAL_ITEMS[0].id);

      cy.get('[data-testid="live-banner"]')
        .should('contain.text', 'Os avisos foram atualizados')
        .and('not.contain.text', 'Há avisos novos');
    });

    it('um aviso fora do formato é ignorado', () => {
      stubMural();
      visitWithFakeHub('/dashboard');
      hubConnected();

      cy.window().then((win) => {
        const sockets = (
          win as unknown as { __urbanHubSockets: { onmessage: (e: { data: string }) => void }[] }
        ).__urbanHubSockets;
        sockets[sockets.length - 1].onmessage({
          data: `${JSON.stringify({ type: 1, target: 'announcementChanged', arguments: ['published'] })}\u001e`,
        });
      });

      cy.get('[data-testid="mural-card"]').should('have.length', MURAL_ITEMS.length);
      cy.get('[data-testid="live-banner"]').should('not.exist');
    });
  });

  describe('síndico que mora no condomínio', () => {
    it('mostra a bolinha no item Mural do menu, e não no Painel de gestão', () => {
      cy.viewport(DESKTOP.width, DESKTOP.height);
      stubSignedIn(MANAGER);
      stubMyAccess('unit');
      stubMural();
      visitWithFakeHub('/mural');

      cy.get('[data-testid="nav-mural"] [data-testid="nav-badge"]').should('contain.text', '3');
      cy.get('[data-testid="nav-dashboard"] [data-testid="nav-badge"]').should('not.exist');
    });
  });

  describe('morador, no celular', () => {
    beforeEach(() => {
      cy.viewport(PHONE.width, PHONE.height);
      stubSignedIn(RESIDENT);
    });

    it('mostra a bolinha no botão do menu, e a faixa cabe na tela', () => {
      const api = stubMural();
      visitWithFakeHub('/dashboard');
      cy.get('[data-testid="menu-toggle-badge"]').should('contain.text', '3');
      hubConnected();

      cy.then(() => api.publish(FRESH));
      pushAnnouncementChange('published', FRESH.id);

      cy.get('[data-testid="live-banner"]').should('be.visible');
      cy.get(
        '[data-testid="live-banner"], [data-testid="live-refresh"], [data-testid="menu-toggle"]',
      ).each(($element) => {
        const { left, right } = $element[0].getBoundingClientRect();
        expect(left).to.be.at.least(0);
        expect(right).to.be.at.most(PHONE.width);
      });
      expectNoHorizontalScroll();
      cy.get('[data-testid="menu-toggle-badge"]').should('contain.text', '4');
    });

    it('a bolinha do botão some quando a gaveta abre, porque o item já mostra a dela', () => {
      stubMural();
      visitWithFakeHub('/dashboard');
      cy.get('[data-testid="menu-toggle-badge"]').should('exist');

      cy.get('[data-testid="menu-toggle"]').click();

      cy.get('[data-testid="menu-toggle-badge"]').should('not.exist');
      cy.get('[data-testid="nav-dashboard"] [data-testid="nav-badge"]').should('contain.text', '3');
    });
  });

  it('segue funcionando quando o servidor de tempo real não responde', () => {
    cy.viewport(DESKTOP.width, DESKTOP.height);
    stubSignedIn(RESIDENT);
    stubMural();

    cy.visit('/dashboard');

    cy.get('[data-testid="mural-card"]').should('have.length', MURAL_ITEMS.length);
    cy.get('[data-testid="summary"]').should(($summary) => {
      expect($summary.text()).to.contain('Você tem 3 avisos novos');
    });
    cy.get('[data-testid="live-banner"]').should('not.exist');
  });
});
