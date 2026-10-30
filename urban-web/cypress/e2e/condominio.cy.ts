import {
  BLUMENAU_ADDRESS,
  NEW_MANAGER,
  stubPostalCode,
  stubPostalCodeFailure,
  stubRegisterCondominium,
  stubRegistrationThatWorks,
} from '../support/condominio';
import { PALETTE, expectErrorsToStayInTheirOwnSpace, themeErrorColor } from '../support/layout';
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

function fillManagerPerson(): void {
  field(manager, 'fullName').click('right').type('Marina Souza');
  field(manager, 'displayName').click('right').type('Marina');
  field(manager, 'cpf').click('right').type('52998224725');
  field(manager, 'birthDate').click('right').type('15061980');
  field(manager, 'phone').click('right').type('47991234567');
}

function fillManager(): void {
  fillManagerPerson();
  fillAddress(manager, '89010000');
}

function fillCondominium(): void {
  field(condominium, 'name').click('right').type('Residencial das Flores');
  field(condominium, 'cnpj').click('right').type('11222333000181');
}

describe('Cadastro do condomínio', () => {
  // Sem este stub, a consulta de CEP iria ao servidor de desenvolvimento, que não tem API.
  beforeEach(() => {
    stubPostalCodeFailure(404, 'condominium.postal_code.not_found');
  });

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

  describe('endereço pelo CEP', () => {
    beforeEach(() => {
      stubSignedIn(NEW_MANAGER);
    });

    for (const theme of ['light', 'dark'] as const) {
      it(`mostra "buscando" em laranja e "preenchido" em verde, com ícone, no tema ${theme}`, () => {
        stubPostalCode(BLUMENAU_ADDRESS, 1500);
        cy.visit(FORM_PATH, {
          onBeforeLoad: (win) => win.localStorage.setItem('urban.theme', theme),
        });

        field(manager, 'postalCode').click('right').type('89010000');

        manager()
          .find('[role="status"] p')
          .should('contain.text', 'Buscando o endereço')
          .and('have.css', 'color', PALETTE[theme].warning)
          .find('mat-icon svg')
          .should('exist');
        cy.wait('@postalCode');
        manager()
          .find('[role="status"] p')
          .should('contain.text', 'Endereço preenchido')
          .and('have.css', 'color', PALETTE[theme].success)
          .find('mat-icon svg')
          .should('exist');
      });
    }

    it('mostra a falha da consulta em vermelho, com ícone, como os outros erros', () => {
      stubPostalCodeFailure(404, 'condominium.postal_code.not_found');
      cy.visit(FORM_PATH);

      field(manager, 'postalCode').click('right').type('89010000');
      cy.wait('@postalCode');

      themeErrorColor().then((expected) => {
        manager()
          .find('[role="alert"]')
          .should('contain.text', 'Não encontramos esse CEP. Preencha o endereço manualmente')
          .and('have.css', 'color', expected)
          .find('mat-icon')
          .should('be.visible');
      });
    });

    it('preenche rua, bairro, cidade e UF ao digitar o CEP e deixa o número para a pessoa', () => {
      stubPostalCode();
      cy.visit(FORM_PATH);

      field(manager, 'postalCode').click('right').type('89010000');
      cy.wait('@postalCode');

      field(manager, 'street').should('have.value', 'Rua XV de Novembro');
      field(manager, 'neighborhood').should('have.value', 'Centro');
      field(manager, 'city').should('have.value', 'Blumenau');
      field(manager, 'state').should('have.value', 'SC');
      field(manager, 'number').should('have.value', '');
      manager().find('[role="status"]').should('contain.text', 'Endereço preenchido');
      field(condominium, 'street').should('have.value', '');
    });

    it('avisa que o CEP não existe e deixa preencher o endereço à mão', () => {
      stubPostalCodeFailure(404, 'condominium.postal_code.not_found');
      cy.visit(FORM_PATH);

      field(manager, 'postalCode').click('right').type('89010000');
      cy.wait('@postalCode');

      manager()
        .find('[role="alert"]')
        .should('contain.text', 'Não encontramos esse CEP. Preencha o endereço manualmente');
      field(manager, 'street')
        .click('right')
        .type('Rua das Flores')
        .should('have.value', 'Rua das Flores');
    });

    it('conclui o cadastro com o endereço trazido pelo CEP', () => {
      stubRegistrationThatWorks();
      stubPostalCode();
      cy.visit(FORM_PATH);

      fillManagerPerson();
      field(manager, 'postalCode').click('right').type('89010000');
      cy.wait('@postalCode');
      field(manager, 'street').should('have.value', 'Rua XV de Novembro');
      field(manager, 'number').click('right').type('120');
      fillCondominium();
      cy.get('[data-testid="same-address"]').click();
      cy.contains('button', 'Cadastrar condomínio').click();

      cy.wait('@registerCondominium').then(({ request }) => {
        expect(request.body.manager.address).to.deep.equal({
          ...BLUMENAU_ADDRESS,
          number: '120',
          complement: null,
        });
        expect(request.body.condominium.address).to.deep.equal(request.body.manager.address);
      });
      cy.location('pathname').should('eq', '/dashboard');
    });
  });

  describe('avisos de erro', () => {
    beforeEach(() => {
      stubSignedIn(NEW_MANAGER);
    });

    // Celular pequeno, celular, tablet e computador: a coluna do Número, a mais estreita, muda de largura em cada um.
    const windows: [number, number][] = [
      [320, 640],
      [375, 667],
      [768, 1024],
      [1280, 800],
    ];
    for (const [width, height] of windows) {
      it(`nenhum aviso invade o campo de baixo com a janela de ${width}x${height}`, () => {
        cy.viewport(width, height);
        stubRegisterCondominium();
        cy.visit(FORM_PATH);

        cy.contains('button', 'Cadastrar condomínio').click();

        cy.contains('mat-error', 'Informe o número (use S/N quando não houver)').should(
          'be.visible',
        );
        expectErrorsToStayInTheirOwnSpace();
      });
    }
  });

  describe('número e complemento no celular', () => {
    for (const width of [320, 375]) {
      it(`mostra os dois rótulos inteiros e o aviso do número em até duas linhas com a janela de ${width}px`, () => {
        cy.viewport(width, 667);
        stubSignedIn(NEW_MANAGER);
        stubRegisterCondominium();
        cy.visit(FORM_PATH);

        cy.contains('button', 'Cadastrar condomínio').click();

        for (const name of ['number', 'complement']) {
          cy.get(`[data-testid="manager-section"] [formControlName="${name}"]`)
            .closest('mat-form-field')
            .find('.mdc-floating-label')
            .then(($label) => {
              expect($label[0].scrollWidth, `rótulo de ${name} cortado`).to.be.at.most(
                $label[0].clientWidth,
              );
            });
        }
        cy.get('[data-testid="manager-section"] [formControlName="number"]')
          .closest('mat-form-field')
          .find('mat-error')
          .then(($error) => {
            // Duas linhas de 20px: numa coluna de um quarto da largura eram sete.
            expect($error[0].getBoundingClientRect().height, 'altura do aviso').to.be.at.most(44);
          });
      });
    }
  });

  describe('espaço entre os campos', () => {
    it('mantém 20px entre dois campos, um sobre o outro, quando não há erro', () => {
      stubSignedIn(NEW_MANAGER);
      cy.viewport(1280, 800);
      cy.visit(FORM_PATH);

      cy.get('[data-testid="manager-section"] [formControlName="street"]').then(($street) => {
        cy.get('[data-testid="manager-section"] [formControlName="number"]').then(($number) => {
          const above = $street.closest('mat-form-field').find('.mdc-notched-outline')[0];
          const below = $number.closest('mat-form-field').find('.mdc-notched-outline')[0];
          expect(
            below.getBoundingClientRect().top - above.getBoundingClientRect().bottom,
          ).to.be.closeTo(20, 1);
        });
      });
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
