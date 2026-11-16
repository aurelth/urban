import { stubDashboard } from '../support/painel';
import { stubSignedIn } from '../support/session';

const MINUTE = 60_000;

/** Relógio falso só para Date e setInterval: a tela de verdade, sem esperar uma hora. */
function openDashboardWithFakeClock(): void {
  cy.clock(Date.now(), ['Date', 'setInterval', 'clearInterval']);
  stubSignedIn();
  stubDashboard();
  cy.visit('/dashboard');
  cy.get('[data-testid="user-menu"]').should('be.visible');
}

describe('Saída por inatividade', () => {
  it('avisa 1 minuto antes e sai da conta ao completar 60 minutos sem mexer', () => {
    openDashboardWithFakeClock();

    cy.tick(59 * MINUTE);

    cy.contains('h2', 'Você ainda está aí?').should('be.visible');
    cy.get('mat-dialog-container').should('contain.text', 'segundos');

    cy.tick(MINUTE);

    cy.location('pathname').should('eq', '/entrar');
    cy.get('[role="status"]').should('contain.text', 'Você saiu por inatividade');
    cy.contains('h2', 'Você ainda está aí?').should('not.exist');
  });

  it('continuar conectado mantém a sessão e reinicia a contagem', () => {
    openDashboardWithFakeClock();
    cy.tick(59 * MINUTE);

    cy.contains('button', 'Continuar conectado').click();

    cy.contains('h2', 'Você ainda está aí?').should('not.exist');
    cy.tick(58 * MINUTE);
    cy.location('pathname').should('eq', '/dashboard');
    cy.contains('h2', 'Você ainda está aí?').should('not.exist');
  });

  it('mexer na tela antes do aviso adia a saída', () => {
    openDashboardWithFakeClock();
    cy.tick(40 * MINUTE);

    cy.get('body').trigger('pointerdown');
    cy.tick(40 * MINUTE);

    cy.location('pathname').should('eq', '/dashboard');
    cy.contains('h2', 'Você ainda está aí?').should('not.exist');
  });

  it('mostra o aviso inteiro no celular, sem rolagem horizontal', () => {
    // 375 px de largura e altura que comporta o painel: com a página mais alta que a janela, o Chromium do Cypress
    // derruba o renderer ao abrir o diálogo com o Date falso (cy.clock). Só acontece no teste: no navegador de verdade e
    // no Playwright com relógio falso o aviso abre normalmente.
    cy.viewport(375, 1400);
    openDashboardWithFakeClock();

    cy.tick(59 * MINUTE);

    cy.contains('button', 'Continuar conectado').should('be.visible');
    cy.document().should((doc) => {
      expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
    });
  });
});
