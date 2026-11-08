import {
  EXPENSES_PATH,
  FIXED_EXPENSE,
  INSTALLMENT_EXPENSE,
  TODAY,
  VARIABLE_EXPENSE,
  stubExpense,
  stubExpenses,
  stubFinanceRejected,
} from '../support/financeiro';
import { PALETTE, expectErrorsToStayInTheirOwnSpace } from '../support/layout';
import { stubSignedIn } from '../support/session';

function rows(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="expense-row"]');
}

/** O rótulo do Material cobre o meio do campo vazio, então o clique vai para a direita (como em "Meus dados"). */
function enter(selector: string, value: string): void {
  cy.get(selector).click('right').type(`{selectall}${value}`);
}

describe('Despesas', () => {
  beforeEach(() => {
    cy.clock(TODAY, ['Date']);
    stubSignedIn();
    stubExpenses();
  });

  it('abre pelo link Financeiro do cabecalho com os grupos e a lista', () => {
    cy.visit('/dashboard');
    cy.get('[data-testid="nav-finance"]').click();

    cy.location('pathname').should('eq', EXPENSES_PATH);
    cy.contains('h1', 'Despesas').should('be.visible');
    cy.get('[data-testid="group-item"]').should('have.length', 2);
    rows().should('have.length', 3);
    rows().first().should('contain.text', 'Portaria').and('contain.text', 'Segura Ltda');
    rows().first().should('contain.text', 'Fixa mensal').and('contain.text', '4.200,00');
    rows().eq(1).should('contain.text', 'Informado a cada mês');
    rows().eq(2).should('contain.text', 'em 6x').and('contain.text', '11/2026 a 04/2027');
  });

  it('cadastra uma despesa fixa pelo dialogo e avisa em verde com texto', () => {
    cy.intercept('POST', '/api/finance/expenses', {
      statusCode: 201,
      body: stubExpense({ description: 'Seguro predial' }),
    }).as('createExpense');
    cy.visit(EXPENSES_PATH);

    cy.get('[data-testid="new-expense"]').click();
    cy.get('urban-expense-dialog h2').should('contain.text', 'Nova despesa');
    enter('urban-expense-dialog [formControlName="description"]', 'Seguro predial');
    enter('urban-expense-dialog [formControlName="amount"]', '900');
    cy.get('urban-expense-dialog').contains('button', 'Cadastrar despesa').click();

    cy.wait('@createExpense').its('request.body').should('deep.equal', {
      kind: 'fixed',
      groupId: '0199bbbb-0001-7000-8000-000000000001',
      description: 'Seguro predial',
      supplier: null,
      startMonth: '2026-11',
      endMonth: null,
      amount: 900,
      installmentCount: null,
      currentInstallment: null,
    });
    cy.get('urban-expense-dialog').should('not.exist');
    cy.get('[data-testid="notice"]')
      .should('contain.text', 'Despesa cadastrada')
      .find('.text-success mat-icon')
      .should('have.css', 'color', PALETTE.light.success);
  });

  it('mostra os campos da parcelada e cadastra com a parcela do mes', () => {
    cy.intercept('POST', '/api/finance/expenses', {
      statusCode: 201,
      body: INSTALLMENT_EXPENSE,
    }).as('createExpense');
    cy.visit(EXPENSES_PATH);

    cy.get('[data-testid="new-expense"]').click();
    cy.get('urban-expense-dialog select[formControlName="kind"]').select('Parcelada');
    cy.get('urban-expense-dialog [formControlName="installmentCount"]').should('be.visible');
    cy.get('urban-expense-dialog [formControlName="endMonth"]').should('not.exist');
    enter('urban-expense-dialog [formControlName="description"]', 'Elevador');
    enter('urban-expense-dialog [formControlName="amount"]', '1000');
    enter('urban-expense-dialog [formControlName="installmentCount"]', '6');
    cy.get('urban-expense-dialog').contains('button', 'Cadastrar despesa').click();

    cy.wait('@createExpense').its('request.body').should('include', {
      kind: 'installment',
      amount: 1000,
      installmentCount: 6,
      currentInstallment: 1,
      startMonth: '2026-11',
    });
  });

  it('mostra os erros do dialogo sem cobrir nada', () => {
    cy.visit(EXPENSES_PATH);

    cy.get('[data-testid="new-expense"]').click();
    cy.get('urban-expense-dialog').contains('button', 'Cadastrar despesa').click();

    cy.get('urban-expense-dialog').should('contain.text', 'Informe a descrição da despesa');
    cy.get('urban-expense-dialog').should('contain.text', 'Informe um valor maior que zero');
    expectErrorsToStayInTheirOwnSpace();
  });

  it('mostra no dialogo o erro que a API devolve', () => {
    stubFinanceRejected(
      'POST',
      '/api/finance/expenses',
      422,
      'expense.end_before_start',
      'createExpense',
    );
    cy.visit(EXPENSES_PATH);

    cy.get('[data-testid="new-expense"]').click();
    enter('urban-expense-dialog [formControlName="description"]', 'Seguro predial');
    enter('urban-expense-dialog [formControlName="amount"]', '900');
    cy.get('urban-expense-dialog').contains('button', 'Cadastrar despesa').click();

    cy.get('urban-expense-dialog [role="alert"]').should(
      'contain.text',
      'O mês final não pode ser anterior ao mês inicial',
    );
  });

  it('edita a descricao levando a versao lida', () => {
    cy.intercept('PUT', `/api/finance/expenses/${FIXED_EXPENSE.id}`, {
      statusCode: 200,
      body: FIXED_EXPENSE,
    }).as('updateExpense');
    cy.visit(EXPENSES_PATH);

    cy.get('button[aria-label="Editar Portaria"]').click();
    cy.get('urban-expense-dialog [formControlName="description"]').should('have.value', 'Portaria');
    enter('urban-expense-dialog [formControlName="description"]', 'Portaria 24h');
    cy.get('urban-expense-dialog').contains('button', 'Salvar alterações').click();

    cy.wait('@updateExpense').its('request.body').should('deep.equal', {
      version: '11',
      description: 'Portaria 24h',
      supplier: 'Segura Ltda',
      groupId: '0199bbbb-0001-7000-8000-000000000001',
    });
    cy.get('urban-expense-dialog').should('not.exist');
  });

  it('reajusta o valor de uma despesa fixa a partir de um mes', () => {
    cy.intercept('POST', `/api/finance/expenses/${FIXED_EXPENSE.id}/amount`, {
      statusCode: 200,
      body: FIXED_EXPENSE,
    }).as('changeAmount');
    cy.visit(EXPENSES_PATH);

    cy.get('button[aria-label="Reajustar valor de Portaria"]').click();
    cy.get('urban-expense-amount-dialog [formControlName="amount"]').should(
      'have.value',
      '4200,00',
    );
    cy.get('urban-expense-amount-dialog select[formControlName="fromMonth"]').select(
      'Janeiro de 2027',
    );
    enter('urban-expense-amount-dialog [formControlName="amount"]', '4400');
    cy.get('urban-expense-amount-dialog').contains('button', 'Reajustar valor').click();

    cy.wait('@changeAmount').its('request.body').should('deep.equal', {
      version: '11',
      fromMonth: '2027-01',
      amount: 4400,
    });
    cy.get('[data-testid="notice"]').should('contain.text', 'Valor reajustado');
  });

  it('altera o valor de uma parcela', () => {
    cy.intercept('PUT', `/api/finance/expenses/${INSTALLMENT_EXPENSE.id}/installments/1`, {
      statusCode: 200,
      body: INSTALLMENT_EXPENSE,
    }).as('setInstallment');
    cy.visit(EXPENSES_PATH);

    cy.get('button[aria-label="Alterar parcela de Elevador"]').click();
    cy.get('urban-installment-dialog [formControlName="amount"]').should('have.value', '166,66');
    enter('urban-installment-dialog [formControlName="amount"]', '170');
    cy.get('urban-installment-dialog').contains('button', 'Salvar parcela').click();

    cy.wait('@setInstallment')
      .its('request.body')
      .should('deep.equal', { version: '13', amount: 170 });
    cy.get('[data-testid="notice"]').should('contain.text', 'Parcela atualizada');
  });

  it('encerra uma despesa variavel no mes escolhido', () => {
    cy.intercept('POST', `/api/finance/expenses/${VARIABLE_EXPENSE.id}/end`, {
      statusCode: 200,
      body: VARIABLE_EXPENSE,
    }).as('endExpense');
    cy.visit(EXPENSES_PATH);

    cy.get('button[aria-label="Encerrar Conta de luz"]').click();
    cy.get('urban-end-expense-dialog').contains('button', 'Encerrar despesa').click();

    cy.wait('@endExpense')
      .its('request.body')
      .should('deep.equal', { version: '12', lastMonth: '2026-11' });
    cy.get('[data-testid="notice"]').should('contain.text', 'Despesa encerrada');
  });

  it('cria e renomeia grupos', () => {
    cy.intercept('POST', '/api/finance/expense-groups', {
      statusCode: 201,
      body: { id: '0199bbbb-0001-7000-8000-000000000003', name: 'Reformas', version: '1' },
    }).as('createGroup');
    cy.intercept('PUT', '/api/finance/expense-groups/0199bbbb-0001-7000-8000-000000000002', {
      statusCode: 200,
      body: { id: '0199bbbb-0001-7000-8000-000000000002', name: 'Obras gerais', version: '7' },
    }).as('renameGroup');
    cy.visit(EXPENSES_PATH);

    cy.get('[data-testid="new-group"]').click();
    enter('urban-group-dialog [formControlName="name"]', 'Reformas');
    cy.get('urban-group-dialog').contains('button', 'Criar grupo').click();
    cy.wait('@createGroup').its('request.body').should('deep.equal', { name: 'Reformas' });
    cy.get('[data-testid="notice"]').should('contain.text', 'Grupo criado');

    cy.get('button[aria-label="Renomear grupo Obras"]').click();
    enter('urban-group-dialog [formControlName="name"]', 'Obras gerais');
    cy.get('urban-group-dialog').contains('button', 'Salvar nome').click();
    cy.wait('@renameGroup')
      .its('request.body')
      .should('deep.equal', { version: '6', name: 'Obras gerais' });
  });

  it('filtra pelas encerradas', () => {
    cy.visit(EXPENSES_PATH);
    rows().should('have.length', 3);

    stubExpenses(
      [stubExpense({ description: 'Jardinagem', isEnded: true, lastMonth: '2026-10' })],
      'endedExpenses',
    );
    cy.get('[data-testid="filter-status"]').select('Encerradas');

    cy.wait('@endedExpenses').its('request.url').should('contain', 'status=ended');
    rows().should('have.length', 1).and('contain.text', 'Encerrada');
    cy.get('button[aria-label="Editar Jardinagem"]').should('exist');
    cy.get('button[aria-label="Encerrar Jardinagem"]').should('not.exist');
  });

  it('mostra o convite para cadastrar quando nao ha despesas', () => {
    stubExpenses([]);
    cy.visit(EXPENSES_PATH);

    cy.get('[data-testid="empty"]').should('contain.text', 'Nenhuma despesa ativa');
  });

  it('cabe numa tela de 360 px, com o dialogo da parcelada inteiro e sem rolagem horizontal', () => {
    cy.viewport(360, 640);
    cy.visit(EXPENSES_PATH);

    rows().should('have.length', 3);
    cy.document().should((doc) => {
      expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
    });

    cy.get('[data-testid="new-expense"]').click();
    cy.get('urban-expense-dialog select[formControlName="kind"]').select('Parcelada');
    cy.get('urban-expense-dialog [formControlName="currentInstallment"]').should('be.visible');
    cy.get('urban-expense-dialog').contains('button', 'Cadastrar despesa').should('be.visible');
    cy.get('urban-expense-dialog').should(($dialog) => {
      expect($dialog[0].scrollWidth).to.be.at.most($dialog[0].clientWidth);
    });
  });
});
