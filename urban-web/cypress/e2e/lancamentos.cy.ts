import {
  EMPTY_MONTH,
  ENTRIES_PATH,
  FIXED_ENTRY,
  OPENED_MONTH,
  PENDING_ENTRY,
  TODAY,
  stubEntry,
  stubFinanceRejected,
  stubMonth,
} from '../support/financeiro';
import { PALETTE, themeErrorColor } from '../support/layout';
import { stubSignedIn } from '../support/session';

const OVERDUE_ENTRY = stubEntry({
  id: '0199bbbb-0003-7000-8000-000000000003',
  description: 'Zeladoria',
  amount: 300,
  dueDate: '2026-11-05',
  status: 'overdue',
  version: '23',
});

const MONTH_WITH_OVERDUE = {
  ...OPENED_MONTH,
  entries: [FIXED_ENTRY, PENDING_ENTRY, OVERDUE_ENTRY],
  total: 4500,
  overdueCount: 1,
};

describe('Lançamentos do mês', () => {
  beforeEach(() => {
    cy.clock(TODAY, ['Date']);
    stubSignedIn();
  });

  it('abre no mes atual, ja com os lancamentos gerados e sem botao de abrir o mes', () => {
    stubMonth();
    cy.visit(ENTRIES_PATH);

    cy.contains('h1', 'Lançamentos do mês').should('be.visible');
    cy.wait('@month').its('request.method').should('eq', 'POST');
    cy.get('[data-testid="month-title"]').should('contain.text', 'Novembro de 2026');
    cy.get('[data-testid="total"]').should('contain.text', '4.200,00');
    cy.get('[data-testid="group-section"] h2').should('contain.text', 'Despesas ordinárias');
    cy.get('[data-testid="entry-row"]').should('have.length', 2);
    cy.get('[data-testid="open-month"]').should('not.exist');
    cy.contains('Abrir mês').should('not.exist');
    cy.contains('Gerar lançamentos que faltam').should('not.exist');
  });

  it('mostra o vencimento de cada lancamento e o valor a informar com a sugestao', () => {
    stubMonth();
    cy.visit(ENTRIES_PATH);

    cy.get('[data-testid="entry-row"]')
      .first()
      .should('contain.text', 'Portaria')
      .and('contain.text', 'Vence em 10/11/2026');
    cy.get('[data-testid="entry-row"]')
      .eq(1)
      .should('contain.text', 'Valor a informar')
      .and('contain.text', 'sugestão')
      .and('contain.text', '812,40')
      .and('contain.text', 'Vence em 12/11/2026');
    cy.get('[data-testid="needs-amount-count"]')
      .should('contain.text', '1 lançamento sem valor')
      .find('.text-warning mat-icon')
      .should('have.css', 'color', PALETTE.light.warning);
  });

  it('mostra o vencido em vermelho, com icone e texto, e conta os vencidos', () => {
    stubMonth(MONTH_WITH_OVERDUE);
    cy.visit(ENTRIES_PATH);

    cy.get('[data-testid="overdue-count"]').should('contain.text', '1 lançamento vencido');
    cy.get('[data-testid="entry-row"]')
      .eq(2)
      .find('button[aria-label^="Situação de Zeladoria"]')
      .should('contain.text', 'Vencido')
      .find('[data-testid="status-badge"]')
      .then(($badge) => {
        themeErrorColor().then((error) => {
          expect(getComputedStyle($badge[0]).color).to.equal(error);
          expect(getComputedStyle($badge.find('mat-icon')[0]).color).to.equal(error);
        });
      });
  });

  it('mantem o titulo e o mes na cor do texto, os rotulos do resumo em cinza e o grupo em destaque', () => {
    stubMonth();
    cy.visit(ENTRIES_PATH);

    for (const selector of ['h1', '[data-testid="month-title"]']) {
      cy.get(selector).first().should('have.css', 'color', PALETTE.light.onSurface);
    }
    for (const selector of [
      '[data-testid="total"] dt',
      '[data-testid="needs-amount-count"] dt',
      '[data-testid="overdue-count"] dt',
    ]) {
      cy.get(selector).first().should('have.css', 'color', PALETTE.light.onSurfaceVariant);
    }
    cy.get('[data-testid="group-section"] h2')
      .first()
      .should('have.css', 'color', PALETTE.light.primary)
      .and('have.css', 'font-weight', '500');
  });

  it('deixa a seta do menu de situacao cinza e so a pinta de indigo enquanto o menu esta aberto', () => {
    stubMonth();
    cy.visit(ENTRIES_PATH);

    const arrow = () =>
      cy.get('button[aria-label^="Situação de Portaria"] [data-testid="status-arrow"]');
    arrow().should('have.css', 'color', PALETTE.light.onSurfaceVariant);

    cy.get('button[aria-label^="Situação de Portaria"]').click();
    cy.get('[role="menuitem"]').should('have.length', 3);
    arrow().should('have.css', 'color', PALETTE.light.primary);

    cy.get('body').type('{esc}');
    cy.get('[role="menuitem"]').should('not.exist');
    arrow().should('have.css', 'color', PALETTE.light.onSurfaceVariant);
  });

  it('pinta o texto e o icone de cada situacao com a mesma cor', () => {
    stubMonth({
      ...OPENED_MONTH,
      entries: [
        { ...FIXED_ENTRY, status: 'paid' },
        stubEntry({
          id: '0199bbbb-0005-7000-8000-000000000005',
          description: 'Elevador',
          amount: 166.66,
          dueDate: '2026-11-20',
          status: 'pending',
          version: '25',
        }),
        stubEntry({
          id: '0199bbbb-0004-7000-8000-000000000004',
          description: 'Jardinagem',
          amount: 150,
          dueDate: '2026-11-25',
          status: 'canceled',
          version: '24',
        }),
      ],
    });
    cy.visit(ENTRIES_PATH);

    const badge = (description: string) =>
      cy.get(`button[aria-label^="Situação de ${description}"] [data-testid="status-badge"]`);

    badge('Portaria')
      .should('contain.text', 'Pago')
      .and('have.css', 'color', PALETTE.light.success)
      .find('mat-icon')
      .should('have.css', 'color', PALETTE.light.success);
    badge('Elevador')
      .should('contain.text', 'Pendente')
      .and('have.css', 'color', PALETTE.light.warning)
      .find('mat-icon')
      .should('have.css', 'color', PALETTE.light.warning);
    badge('Jardinagem')
      .should('contain.text', 'Cancelado')
      .then(($badge) => {
        themeErrorColor().then((error) => {
          expect(getComputedStyle($badge[0]).color).to.equal(error);
          expect(getComputedStyle($badge.find('mat-icon')[0]).color).to.equal(error);
        });
      });
  });

  it('pinta cada opcao do menu de situacao com a cor do seu icone', () => {
    stubMonth();
    cy.visit(ENTRIES_PATH);

    cy.get('button[aria-label^="Situação de Portaria"]').click();

    const option = (label: string) => cy.get('[role="menuitem"]').contains(label);
    const icon = (label: string) =>
      cy.get('[role="menuitem"]').contains(label).closest('[role="menuitem"]').find('mat-icon');

    option('Pendente').should('have.css', 'color', PALETTE.light.warning);
    icon('Pendente').should('have.css', 'color', PALETTE.light.warning);
    option('Pago').should('have.css', 'color', PALETTE.light.success);
    icon('Pago').should('have.css', 'color', PALETTE.light.success);
    themeErrorColor().then((error) => {
      option('Cancelado').should('have.css', 'color', error);
      icon('Cancelado').should('have.css', 'color', error);
    });
  });

  it('marca um lancamento como pago pelo menu e atualiza o mes', () => {
    stubMonth();
    cy.intercept('PUT', `/api/finance/entries/${FIXED_ENTRY.id}/status`, {
      statusCode: 200,
      body: { ...FIXED_ENTRY, status: 'paid' },
    }).as('setStatus');
    cy.visit(ENTRIES_PATH);

    cy.get('button[aria-label^="Situação de Portaria"]').should('contain.text', 'Pendente').click();
    cy.get('[role="menuitem"]').contains('Pago').click();

    cy.wait('@setStatus')
      .its('request.body')
      .should('deep.equal', { version: '21', status: 'paid' });
    cy.wait('@monthRead');
    cy.get('[data-testid="notice"]').should('not.exist');
  });

  it('nao deixa marcar como pago o que ainda espera o valor', () => {
    stubMonth();
    cy.visit(ENTRIES_PATH);

    cy.get('button[aria-label^="Situação de Conta de luz"]').click();

    cy.get('[role="menuitem"]')
      .contains('Pago')
      .closest('button')
      .should('be.disabled')
      .and('contain.text', 'Informe o valor primeiro');
  });

  it('cancela um lancamento e explica quando a API recusa', () => {
    stubMonth();
    stubFinanceRejected(
      'PUT',
      `/api/finance/entries/${FIXED_ENTRY.id}/status`,
      422,
      'expense_entry.amount_required',
      'setStatus',
    );
    cy.visit(ENTRIES_PATH);

    cy.get('button[aria-label^="Situação de Portaria"]').click();
    cy.get('[role="menuitem"]').contains('Cancelado').click();

    cy.wait('@setStatus')
      .its('request.body')
      .should('deep.equal', { version: '21', status: 'canceled' });
    cy.get('[data-testid="action-error"]').should(
      'contain.text',
      'Informe o valor do lançamento antes de marcá-lo como pago',
    );
  });

  it('avisa quando o mes nao tem lancamentos', () => {
    stubMonth(EMPTY_MONTH);
    cy.visit(ENTRIES_PATH);

    cy.get('[data-testid="empty"]').should('contain.text', 'Este mês não tem lançamentos');
  });

  it('mostra o erro quando nao consegue abrir o mes e tenta de novo', () => {
    stubFinanceRejected('POST', /^\/api\/finance\/months\/2026-11\/open$/, 500, 'x', 'month');
    cy.visit(ENTRIES_PATH);
    cy.get('[data-testid="load-error"]').should('be.visible');

    stubMonth();
    cy.get('[data-testid="retry"]').click();

    cy.get('[data-testid="entry-row"]').should('have.length', 2);
  });

  it('informa o valor de um lancamento sem valor com a sugestao do mes anterior', () => {
    stubMonth();
    cy.intercept('PUT', `/api/finance/entries/${PENDING_ENTRY.id}/amount`, {
      statusCode: 200,
      body: { ...PENDING_ENTRY, amount: 830.1, needsAmount: false },
    }).as('setAmount');
    cy.visit(ENTRIES_PATH);

    cy.get('button[aria-label="Informar valor de Conta de luz"]').click();
    cy.get('urban-entry-amount-dialog [formControlName="amount"]').should('have.value', '812,40');
    cy.get('urban-entry-amount-dialog [formControlName="amount"]')
      .click('right')
      .type('{selectall}830,10');
    cy.get('urban-entry-amount-dialog').contains('button', 'Informar valor').click();

    cy.wait('@setAmount')
      .its('request.body')
      .should('deep.equal', { version: '22', amount: 830.1 });
    cy.get('urban-entry-amount-dialog').should('not.exist');
    cy.get('[data-testid="notice"]').should('contain.text', 'Valor informado');
  });

  it('navega entre os meses, gerando os lancamentos de cada um', () => {
    stubMonth();
    cy.visit(ENTRIES_PATH);
    cy.wait('@month').its('request.url').should('contain', '/months/2026-11/open');

    cy.get('button[aria-label="Mês anterior"]').click();
    cy.wait('@month').its('request.url').should('contain', '/months/2026-10/open');
    cy.get('[data-testid="month-title"]').should('contain.text', 'Outubro de 2026');

    cy.get('button[aria-label="Próximo mês"]').click();
    cy.get('button[aria-label="Próximo mês"]').click();
    cy.get('[data-testid="month-title"]').should('contain.text', 'Dezembro de 2026');
    cy.get('button[aria-label="Próximo mês"]').should('not.be.disabled');
  });

  it('volta para as despesas pelo link', () => {
    stubMonth();
    cy.visit(ENTRIES_PATH);

    cy.get('[data-testid="expenses-link"]').click();

    cy.location('pathname').should('eq', '/financeiro/despesas');
  });

  it('cabe numa tela de 360 px, sem rolagem horizontal da pagina', () => {
    cy.viewport(360, 640);
    stubMonth(MONTH_WITH_OVERDUE);
    cy.visit(ENTRIES_PATH);

    cy.get('[data-testid="entry-row"]').should('have.length', 3);
    cy.document().should((doc) => {
      expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
    });
  });
});
