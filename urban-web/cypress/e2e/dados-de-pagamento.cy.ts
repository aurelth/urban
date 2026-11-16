import {
  INSTRUCTIONS_API,
  INSTRUCTIONS_PATH,
  NO_INSTRUCTIONS,
  enter,
  stubInstructions,
  stubInstructionsRejected,
  stubInstructionsSaves,
} from '../support/dados-pagamento';
import { PALETTE, expectErrorsToStayInTheirOwnSpace, themeErrorColor } from '../support/layout';
import { chooseMenu } from '../support/menu';
import { stubSignedIn } from '../support/session';

function pickKind(label: string): void {
  chooseMenu('urban-menu-select[formControlName="pixKind"]', label);
}

function pickAccountKind(label: string): void {
  chooseMenu('urban-menu-select[formControlName="accountKind"]', label);
}

function save(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.contains('button', 'Salvar alterações');
}

describe('Dados para pagamento', () => {
  beforeEach(() => {
    stubSignedIn();
  });

  it('abre com a chave Pix e a conta cadastradas, e a data da última atualização', () => {
    stubInstructions();
    cy.visit(INSTRUCTIONS_PATH);

    cy.contains('h1', 'Dados para pagamento').should('be.visible');
    cy.get('input[formControlName="pixKey"]').should('have.value', '11.222.333/0001-81');
    cy.get('input[formControlName="bank"]').should('have.value', 'Banco do Brasil');
    cy.get('input[formControlName="agency"]').should('have.value', '1234-5');
    cy.get('input[formControlName="number"]').should('have.value', '98765-4');
    cy.get('[data-testid="updated-at"]').should('contain.text', '16/11/2026 às 10:30');
    save().should('be.disabled');
  });

  it('sem nada cadastrado, so mostra o campo da chave depois de escolher o tipo', () => {
    stubInstructions(NO_INSTRUCTIONS);
    cy.visit(INSTRUCTIONS_PATH);

    cy.get('input[formControlName="pixKey"]').should('not.exist');
    cy.get('[data-testid="updated-at"]').should('not.exist');

    pickKind('CPF');
    enter('pixKey', '52998224725');

    cy.get('input[formControlName="pixKey"]').should('have.value', '529.982.247-25');
  });

  it('trocar o tipo da chave apaga o que estava digitado', () => {
    stubInstructions();
    cy.visit(INSTRUCTIONS_PATH);

    pickKind('E-mail');

    cy.get('input[formControlName="pixKey"]').should('have.value', '');
  });

  it('salva a chave sem mascara e a conta, e avisa em verde', () => {
    stubInstructions(NO_INSTRUCTIONS);
    stubInstructionsSaves();
    cy.visit(INSTRUCTIONS_PATH);

    pickKind('Celular');
    enter('pixKey', '47991234567');
    enter('bank', 'Caixa Econômica');
    enter('agency', '0001');
    enter('number', '12345-6');
    pickAccountKind('Poupança');
    save().click();

    cy.wait('@saveInstructions')
      .its('request.body')
      .should('deep.equal', {
        pix: { kind: 'phone', key: '47991234567' },
        account: { bank: 'Caixa Econômica', agency: '0001', number: '12345-6', kind: 'savings' },
      });
    cy.get('[data-testid="saved"]')
      .should('contain.text', 'Dados salvos')
      .find('.text-success mat-icon')
      .should('have.css', 'color', PALETTE.light.success);
    cy.get('[data-testid="updated-at"]').should('contain.text', '16/11/2026 às 11:00');
    cy.get('input[formControlName="pixKey"]').should('have.value', '(47) 99123-4567');
    save().should('be.disabled');
  });

  it('"Não informar" tira a chave Pix e a conta em branco tira a conta', () => {
    stubInstructions();
    stubInstructionsSaves();
    cy.visit(INSTRUCTIONS_PATH);

    pickKind('Não informar');
    cy.get('input[formControlName="bank"]').click('right').type('{selectall}{del}');
    enter('agency', '{del}');
    cy.get('input[formControlName="number"]').click('right').type('{selectall}{del}');
    pickAccountKind('Não informar');
    save().click();

    cy.wait('@saveInstructions')
      .its('request.body')
      .should('deep.equal', { pix: null, account: null });
    cy.get('input[formControlName="pixKey"]').should('not.exist');
  });

  it('nao chama a API com a chave incompleta e leva o foco ao primeiro campo com erro', () => {
    stubInstructions();
    stubInstructionsSaves();
    cy.visit(INSTRUCTIONS_PATH);

    enter('pixKey', '11222333');
    enter('agency', 'abc');
    save().click();

    cy.get('mat-error').should('contain.text', 'Informe um CNPJ válido, com 14 caracteres');
    cy.get('mat-error').should('contain.text', 'Informe a agência com até 5 dígitos');
    cy.focused().should('have.attr', 'formControlName', 'pixKey');
    expectErrorsToStayInTheirOwnSpace();
    cy.get('@saveInstructions.all').should('have.length', 0);
  });

  it('conta comecada pede o que falta', () => {
    stubInstructions(NO_INSTRUCTIONS);
    stubInstructionsSaves();
    cy.visit(INSTRUCTIONS_PATH);

    enter('bank', 'Caixa Econômica');
    cy.get('form').submit();

    cy.get('mat-error')
      .should('contain.text', 'Informe a agência')
      .and('contain.text', 'Informe o número da conta')
      .and('contain.text', 'Escolha o tipo da conta');
    cy.get('@saveInstructions.all').should('have.length', 0);
  });

  it('mostra em vermelho, com ícone, o texto da recusa da API e mantem o que foi digitado', () => {
    stubInstructions();
    stubInstructionsRejected('finance.payment_instructions.pix_key_invalid');
    cy.visit(INSTRUCTIONS_PATH);

    enter('agency', '9999');
    save().click();

    cy.wait('@saveInstructions');
    themeErrorColor().then((expected) => {
      cy.get('urban-form-submit [role="alert"]')
        .should('contain.text', 'A chave Pix não é válida para o tipo escolhido')
        .and('have.css', 'color', expected)
        .find('mat-icon')
        .should('be.visible');
    });
    cy.get('input[formControlName="agency"]').should('have.value', '9999');
    cy.get('[data-testid="saved"]').should('not.exist');
  });

  it('avisa quando os dados nao carregam e deixa tentar de novo', () => {
    cy.intercept('GET', INSTRUCTIONS_API, { statusCode: 503, body: {} }).as('failed');
    cy.visit(INSTRUCTIONS_PATH);

    cy.get('[data-testid="load-error"]').should(
      'contain.text',
      'Não foi possível carregar os dados para pagamento',
    );

    stubInstructions();
    cy.get('[data-testid="retry"]').click();

    cy.get('input[formControlName="bank"]').should('have.value', 'Banco do Brasil');
    cy.get('[data-testid="load-error"]').should('not.exist');
  });
});
