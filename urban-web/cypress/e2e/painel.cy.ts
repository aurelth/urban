import { PALETTE } from '../support/layout';
import { INSTRUCTIONS_API, NO_INSTRUCTIONS } from '../support/dados-pagamento';
import { MONTH_URL, PAYMENTS_URL, failOnce, stubDashboard } from '../support/painel';
import { stubMural } from '../support/mural';
import { RESIDENT, stubSignedIn } from '../support/session';

describe('Painel', () => {
  it('abre o painel na rota inicial, com os quatro cartões do síndico', () => {
    stubSignedIn();
    stubDashboard();
    cy.visit('/');

    cy.location('pathname').should('eq', '/dashboard');
    cy.get('h1').should('have.text', 'Painel').and('have.class', 'sr-only');
    cy.get('main').should('not.contain.text', 'Gestão condominial');
    for (const card of ['card-month', 'card-payments', 'card-condominium', 'card-pendings']) {
      cy.get(`[data-testid="${card}"]`).should('be.visible');
    }
  });

  describe('cartões', () => {
    it('mostra o mês aberto com o total das despesas e leva aos lançamentos', () => {
      stubSignedIn();
      stubDashboard({ month: 'open' });
      cy.visit('/dashboard');

      cy.get('[data-testid="month-state"]').should('have.text', 'Aberto');
      cy.get('[data-testid="month-total"]')
        .invoke('text')
        .should('match', /R\$\s*4\.200,00/);
      cy.get('[data-testid="card-payments"]').should(
        'contain.text',
        'As cobranças aparecem depois que o mês for fechado',
      );
      cy.get('[data-testid="month-link"]')
        .should('have.attr', 'href', '/financeiro/lancamentos')
        .click();
      cy.location('pathname').should('eq', '/financeiro/lancamentos');
    });

    it('mostra o mês fechado, o vencimento e o quanto já foi recebido', () => {
      stubSignedIn();
      stubDashboard({ month: 'closed', overdueCharges: 3 });
      cy.visit('/dashboard');

      cy.get('[data-testid="month-state"]').should('have.text', 'Fechado');
      cy.get('[data-testid="month-due"]').should('have.text', '10/12/2026');
      cy.get('[data-testid="received-percent"]').should('have.text', '54% recebido');
      cy.get('[role="progressbar"]').should('have.attr', 'aria-valuenow', '54');
      cy.get('[data-testid="charges-overdue"]').should('have.text', '3 cobranças em atraso');
    });

    it('mostra quantas unidades e moradores há, com links para as telas', () => {
      stubSignedIn();
      stubDashboard({ activeUnits: 40, residents: 37 });
      cy.visit('/dashboard');

      cy.get('[data-testid="stat-units"]')
        .should('contain.text', '40')
        .and('have.attr', 'href', '/unidades');
      cy.get('[data-testid="stat-residents"]')
        .should('contain.text', '37')
        .and('have.attr', 'href', '/moradores');
    });
  });

  describe('pendências', () => {
    it('diz que está tudo em dia quando nada falta', () => {
      stubSignedIn();
      stubDashboard({ month: 'closed' });
      cy.visit('/dashboard');

      cy.get('[data-testid="all-clear"]')
        .should('contain.text', 'Tudo em dia')
        .find('mat-icon')
        .should('have.css', 'color', PALETTE.light.success);
      cy.get('[data-testid="pending"]').should('not.exist');
    });

    it('lista cada pendência com ícone laranja e o link que a resolve', () => {
      stubSignedIn();
      stubDashboard({
        month: 'open',
        needsAmountCount: 2,
        apportionmentReady: false,
        instructions: NO_INSTRUCTIONS,
      });
      cy.visit('/dashboard');

      cy.get('[data-testid="pending"]').should('have.length', 3);
      cy.get('[data-testid="pending"]')
        .first()
        .find('mat-icon')
        .should('have.css', 'color', PALETTE.light.warning);
      cy.contains('[data-testid="pending"]', 'Os dados de pagamento ainda não foram informados')
        .find('a')
        .should('have.attr', 'href', '/financeiro/dados-de-pagamento')
        .click();
      cy.location('pathname').should('eq', '/financeiro/dados-de-pagamento');
    });

    it('não diz que está tudo em dia quando uma leitura falha, e deixa tentar de novo', () => {
      stubSignedIn();
      stubDashboard({ month: 'closed' });
      failOnce(INSTRUCTIONS_API, 'instructionsFailed');
      cy.visit('/dashboard');

      cy.get('[data-testid="all-clear"]').should('not.exist');
      cy.get('[data-testid="pendings-incomplete"]')
        .should('contain.text', 'Não foi possível verificar tudo')
        .find('[data-testid="retry"]')
        .click();

      cy.get('[data-testid="pendings-incomplete"]').should('not.exist');
      cy.get('[data-testid="all-clear"]').should('be.visible');
    });
  });

  describe('quando uma chamada falha', () => {
    it('mostra o erro só no cartão do mês e deixa tentar de novo, sem derrubar os outros', () => {
      stubSignedIn();
      stubDashboard();
      failOnce(MONTH_URL, 'monthFailed');
      cy.visit('/dashboard');

      cy.get('[data-testid="card-month"]')
        .should('contain.text', 'Não foi possível carregar o mês')
        .find('mat-icon')
        .should('have.css', 'color', 'rgb(215, 0, 21)');
      cy.get('[data-testid="stat-units"]').should('contain.text', '40');

      cy.get('[data-testid="card-month"] [data-testid="retry"]').click();

      cy.get('[data-testid="month-state"]').should('have.text', 'Aberto');
    });

    it('mostra o erro só no cartão de recebimentos quando as cobranças não carregam', () => {
      stubSignedIn();
      stubDashboard({ month: 'closed' });
      failOnce(PAYMENTS_URL, 'paymentsFailed');
      cy.visit('/dashboard');

      cy.get('[data-testid="card-payments"]').should(
        'contain.text',
        'Não foi possível carregar os recebimentos',
      );
      cy.get('[data-testid="month-state"]').should('have.text', 'Fechado');
    });
  });

  it('mostra o mural para o morador, no lugar do painel, sem chamar a API do condomínio', () => {
    stubSignedIn(RESIDENT);
    stubDashboard();
    stubMural();
    cy.visit('/dashboard');

    cy.contains('h1', 'Mural').should('be.visible');
    cy.get('[data-testid="mural-card"]').should('have.length.greaterThan', 0);
    cy.get('[data-testid="welcome"]').should('not.exist');
    cy.get('[data-testid="card-month"]').should('not.exist');
    cy.get('@month.all').should('have.length', 0);
  });

  it('cumprimenta pelo nome escolhido e mostra o papel no cabeçalho', () => {
    stubSignedIn();
    cy.visit('/dashboard');

    cy.get('[data-testid="current-user"]')
      .should('have.text', 'Olá Marina - Síndico')
      .and('not.contain.text', '!')
      .and('not.contain.text', 'sindica@example.com');
  });

  it('não separa o cabeçalho do corpo com uma linha, nem no tema claro nem no escuro', () => {
    stubSignedIn();
    cy.visit('/dashboard', {
      onBeforeLoad: (win) => win.localStorage.setItem('urban.theme', 'light'),
    });

    cy.get('mat-toolbar')
      .should('have.css', 'border-bottom-width', '0px')
      .and('have.css', 'border-top-width', '0px')
      .and('have.css', 'background-color', PALETTE.light.card);

    cy.get('button[aria-label="Usar tema escuro"]').click();

    cy.get('html').should('have.class', 'app-dark');
    cy.get('mat-toolbar').should('have.css', 'border-bottom-width', '0px');
  });

  it('alterna entre tema claro e escuro', () => {
    stubSignedIn();
    // Parte sempre do tema claro, independente da preferencia do sistema de quem roda o teste.
    cy.visit('/', {
      onBeforeLoad: (win) => win.localStorage.setItem('urban.theme', 'light'),
    });
    cy.get('html').should('not.have.class', 'app-dark');

    cy.get('button[aria-label="Usar tema escuro"]').click();
    cy.get('html').should('have.class', 'app-dark');

    cy.get('button[aria-label="Usar tema claro"]').click();
    cy.get('html').should('not.have.class', 'app-dark');
  });
});
