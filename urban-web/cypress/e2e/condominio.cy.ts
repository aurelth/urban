import {
  NEW_MANAGER,
  stubRegisterCondominium,
  stubRegistrationThatWorks,
} from '../support/condominio';
import { MANAGER, stubSignedIn } from '../support/session';

const FORM_PATH = '/condominio/cadastrar';

function manager(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="manager-section"]');
}

function condominium(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="condominium-section"]');
}

// Os campos são tocados na ponta direita: o rótulo do Material fica por cima do campo vazio e, nos
// rótulos longos ("Data de nascimento"), chega ao centro, que é onde o Cypress clicaria.
function field(
  section: () => Cypress.Chainable<JQuery<HTMLElement>>,
  name: string,
): Cypress.Chainable<JQuery<HTMLElement>> {
  return section().find(`[formControlName="${name}"]`);
}

function fillAddress(section: () => Cypress.Chainable<JQuery<HTMLElement>>, cep: string): void {
  field(section, 'postalCode').click('right').type(cep);
  field(section, 'street').click('right').type('Rua das Flores');
  field(section, 'number').click('right').type('120');
  field(section, 'complement').click('right').type('Apto 42');
  field(section, 'neighborhood').click('right').type('Centro');
  field(section, 'city').click('right').type('Blumenau');
  field(section, 'state').select('SC');
}

function fillManager(): void {
  field(manager, 'fullName').click('right').type('Marina Souza');
  field(manager, 'displayName').click('right').type('Marina');
  field(manager, 'cpf').click('right').type('52998224725');
  field(manager, 'birthDate').click('right').type('15061980');
  field(manager, 'phone').click('right').type('47991234567');
  fillAddress(manager, '89010000');
}

function fillCondominium(): void {
  field(condominium, 'name').click('right').type('Residencial das Flores');
  field(condominium, 'cnpj').click('right').type('11222333000181');
}

describe('Cadastro do condomínio', () => {
  describe('quem chega ao formulário', () => {
    it('leva o síndico que ainda não cadastrou o condomínio ao formulário', () => {
      stubSignedIn(NEW_MANAGER);
      cy.visit('/');

      cy.location('pathname').should('eq', FORM_PATH);
      cy.get('h1').should('have.text', 'Cadastre seu condomínio');
      cy.get('[data-testid="current-user"]').should('contain.text', 'sindica@example.com');
    });

    it('leva ao formulário também quem tenta abrir o painel', () => {
      stubSignedIn(NEW_MANAGER);
      cy.visit('/dashboard');

      cy.location('pathname').should('eq', FORM_PATH);
    });

    it('manda quem já tem condomínio de volta ao painel', () => {
      stubSignedIn(MANAGER);
      cy.visit(FORM_PATH);

      cy.location('pathname').should('eq', '/dashboard');
    });
  });

  describe('preenchimento', () => {
    beforeEach(() => {
      stubSignedIn(NEW_MANAGER);
      cy.visit(FORM_PATH);
    });

    it('formata CPF, data, celular, CEP e CNPJ enquanto a pessoa digita', () => {
      field(manager, 'cpf')
        .click('right')
        .type('52998224725')
        .should('have.value', '529.982.247-25');
      field(manager, 'birthDate')
        .click('right')
        .type('15061980')
        .should('have.value', '15/06/1980');
      field(manager, 'phone')
        .click('right')
        .type('47991234567')
        .should('have.value', '(47) 99123-4567');
      field(manager, 'postalCode')
        .click('right')
        .type('89010000')
        .should('have.value', '89010-000');
      field(condominium, 'cnpj')
        .click('right')
        .type('12abc34501de35')
        .should('have.value', '12.ABC.345/01DE-35');
    });

    it('mantém o cursor no lugar ao corrigir um dígito no meio do CPF', () => {
      field(manager, 'cpf').click('right').type('52998224725');
      field(manager, 'cpf')
        .click('right')
        .type('{leftarrow}{leftarrow}{leftarrow}{leftarrow}{backspace}8');

      field(manager, 'cpf').should('have.value', '529.982.287-25');
    });

    it('mostra o que falta e leva o foco ao primeiro campo, sem enviar nada', () => {
      stubRegisterCondominium();
      cy.contains('button', 'Cadastrar condomínio').click();

      cy.contains('mat-error', 'Informe o nome completo').should('be.visible');
      cy.contains('mat-error', 'Informe o CPF').should('be.visible');
      cy.contains('mat-error', 'Informe o CNPJ').should('be.visible');
      cy.focused().should('have.attr', 'formcontrolname', 'fullName');
      cy.get('@registerCondominium.all').should('have.length', 0);
    });

    it('avisa o CPF inválido, o celular fixo e a idade', () => {
      field(manager, 'cpf').click('right').type('11111111111');
      field(manager, 'phone').click('right').type('4731234567');
      field(manager, 'birthDate').click('right').type('15062020').blur();

      cy.contains('mat-error', 'Informe um CPF válido').should('be.visible');
      cy.contains('mat-error', 'Informe um celular com DDD').should('be.visible');
      cy.contains('mat-error', 'O síndico precisa ter 18 anos ou mais').should('be.visible');
    });

    it('esconde o endereço do condomínio quando é o mesmo do síndico', () => {
      condominium().find('urban-address-fields').should('exist');

      cy.get('[data-testid="same-address"]').click();

      condominium().find('urban-address-fields').should('not.exist');
      cy.get('[data-testid="same-address"]').click();
      condominium().find('urban-address-fields').should('exist');
    });
  });

  describe('envio', () => {
    it('cadastra, renova a sessão e abre o painel cumprimentando pelo nome', () => {
      stubSignedIn(NEW_MANAGER);
      stubRegistrationThatWorks();
      cy.visit(FORM_PATH);

      fillManager();
      fillCondominium();
      fillAddress(condominium, '89012100');
      cy.contains('button', 'Cadastrar condomínio').click();

      cy.wait('@registerCondominium')
        .its('request.body')
        .should('deep.equal', {
          displayName: 'Marina',
          manager: {
            fullName: 'Marina Souza',
            cpf: '52998224725',
            birthDate: '1980-06-15',
            phone: '47991234567',
            address: {
              postalCode: '89010000',
              street: 'Rua das Flores',
              number: '120',
              complement: 'Apto 42',
              neighborhood: 'Centro',
              city: 'Blumenau',
              state: 'SC',
            },
          },
          condominium: {
            name: 'Residencial das Flores',
            cnpj: '11222333000181',
            address: {
              postalCode: '89012100',
              street: 'Rua das Flores',
              number: '120',
              complement: 'Apto 42',
              neighborhood: 'Centro',
              city: 'Blumenau',
              state: 'SC',
            },
          },
        });
      cy.location('pathname').should('eq', '/dashboard');
      cy.get('[data-testid="current-user"]')
        .should('contain.text', 'Olá Marina!')
        .and('contain.text', 'Síndico');
    });

    it('envia o endereço do síndico nos dois quando o do condomínio é o mesmo', () => {
      stubSignedIn(NEW_MANAGER);
      stubRegistrationThatWorks();
      cy.visit(FORM_PATH);

      fillManager();
      fillCondominium();
      cy.get('[data-testid="same-address"]').click();
      cy.contains('button', 'Cadastrar condomínio').click();

      cy.wait('@registerCondominium').then(({ request }) => {
        expect(request.body.condominium.address).to.deep.equal(request.body.manager.address);
      });
      cy.location('pathname').should('eq', '/dashboard');
    });

    it('explica que o CNPJ já está cadastrado e deixa o formulário como está', () => {
      stubSignedIn(NEW_MANAGER);
      stubRegisterCondominium(409, 'condominium.cnpj.already_registered');
      cy.visit(FORM_PATH);

      fillManager();
      fillCondominium();
      cy.get('[data-testid="same-address"]').click();
      cy.contains('button', 'Cadastrar condomínio').click();

      cy.get('[role="alert"]').should(
        'contain.text',
        'Este CNPJ já está cadastrado. Peça um convite ao síndico atual',
      );
      cy.location('pathname').should('eq', FORM_PATH);
      field(manager, 'fullName').should('have.value', 'Marina Souza');
      cy.contains('button', 'Cadastrar condomínio').should('be.enabled');
    });
  });
});
