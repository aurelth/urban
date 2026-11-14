import { chooseMenu } from '../support/menu';
import { DESKTOP, PHONE } from '../support/navegacao';
import { MANAGER, stubSignedIn } from '../support/session';
import { stubUnits } from '../support/unidades';
import {
  CURRENT_RESIDENT,
  NEW_RESIDENT_PATH,
  RESIDENTS_PATH,
  editResidentPath,
  fillResidentTexts,
  stubResidentCreateRejected,
  stubResidentCreated,
  stubResidentDetail,
  stubResidentUpdateRejected,
  stubResidentUpdated,
  stubResidents,
  typeInto,
} from '../support/moradores';

function chooseUnitAndKind(): void {
  chooseMenu('[formControlName="unitId"]', 'Bloco A · 101');
  chooseMenu('[formControlName="kind"]', 'Inquilino');
}

describe('Formulário de morador', () => {
  beforeEach(() => {
    stubSignedIn(MANAGER);
    stubUnits();
    stubResidents();
  });

  describe('cadastro', () => {
    it('abre pelo botão "Novo morador" da lista, cadastra e mostra a confirmação', () => {
      stubResidentCreated();
      cy.viewport(DESKTOP.width, DESKTOP.height);
      cy.visit(RESIDENTS_PATH);

      cy.get('[data-testid="new-resident"]').click();
      cy.location('pathname').should('eq', NEW_RESIDENT_PATH);
      cy.contains('h1', 'Novo morador').should('be.visible');

      fillResidentTexts();
      typeInto('email', 'maria@exemplo.com');
      chooseUnitAndKind();
      cy.contains('button[type="submit"]', 'Cadastrar morador').click();

      cy.wait('@createResident')
        .its('request.body')
        .should('deep.equal', {
          fullName: 'Maria Souza',
          cpf: '52998224725',
          birthDate: '1990-05-20',
          phone: '47998765432',
          email: 'maria@exemplo.com',
          profession: 'Engenheira',
          emergencyContact: { name: 'Carlos Souza', relationship: 'Irmão', phone: '4733334444' },
          unitId: '0199aaaa-0000-7000-8000-000000000001',
          kind: 'tenant',
          startDate: '2024-03-01',
        });
      cy.get('[data-testid="created"]').should('contain.text', 'Morador cadastrado');
      cy.get('[data-testid="created"]').should('contain.text', 'Bloco A · 101');
      cy.get('[data-testid="residents-link"]').should('have.attr', 'href', RESIDENTS_PATH);
    });

    it('mostra o que falta quando se envia vazio e não chama a API', () => {
      stubResidentCreated();
      cy.visit(NEW_RESIDENT_PATH);

      cy.contains('button[type="submit"]', 'Cadastrar morador').click();

      cy.contains('mat-error', 'Informe o nome completo').should('be.visible');
      cy.contains('mat-error', 'Escolha a unidade').should('be.visible');
      cy.get('@createResident.all').should('have.length', 0);
    });

    it('avisa do CPF repetido sem apagar o que foi digitado', () => {
      stubResidentCreateRejected(409, 'condominium.cpf.already_registered');
      cy.visit(NEW_RESIDENT_PATH);
      fillResidentTexts();
      chooseUnitAndKind();

      cy.contains('button[type="submit"]', 'Cadastrar morador').click();

      cy.get('[role="alert"]').should(
        'contain.text',
        'Este CPF já está cadastrado neste condomínio',
      );
      cy.get('[formControlName="fullName"]').should('have.value', 'Maria Souza');
    });

    it('sem unidade ativa explica e leva ao cadastro de unidades', () => {
      stubUnits([]);
      cy.visit(NEW_RESIDENT_PATH);

      cy.get('[data-testid="no-units"]').should('contain.text', 'Cadastre ao menos uma unidade');
      cy.get('[data-testid="units-link"]').should('have.attr', 'href', '/unidades');
    });
  });

  describe('edição', () => {
    it('abre pelo link "Editar" da lista, com os dados preenchidos e o CPF mascarado', () => {
      stubResidentDetail();
      cy.viewport(DESKTOP.width, DESKTOP.height);
      cy.visit(RESIDENTS_PATH);

      cy.get('[data-testid="edit-resident"]').first().click();

      cy.location('pathname').should('eq', editResidentPath(CURRENT_RESIDENT.id));
      cy.contains('h1', 'Editar morador').should('be.visible');
      cy.get('[formControlName="fullName"]').should('have.value', 'Ana Lima');
      cy.get('[formControlName="birthDate"]').should('have.value', '20/05/1990');
      cy.get('[formControlName="phone"]').should('have.value', '(47) 99876-5432');
      cy.get('[data-testid="cpf-masked"]').should('have.text', '***.123.456-**');
      cy.contains('button[type="submit"]', 'Salvar alterações').should('be.disabled');
    });

    it('salva a mudança com a versão lida, sem CPF, e avisa', () => {
      stubResidentDetail();
      stubResidentUpdated(CURRENT_RESIDENT, { profession: 'Médica' });
      cy.visit(editResidentPath(CURRENT_RESIDENT.id));

      typeInto('profession', 'Médica');
      cy.contains('button[type="submit"]', 'Salvar alterações').click();

      cy.wait('@updateResident')
        .its('request.body')
        .should('deep.equal', {
          version: '7',
          fullName: 'Ana Lima',
          birthDate: '1990-05-20',
          phone: '47998765432',
          email: 'ana@exemplo.com',
          profession: 'Médica',
          emergencyContact: { name: 'Carlos Lima', relationship: 'Irmão', phone: '4733334444' },
        });
      cy.get('[data-testid="saved"]').should('contain.text', 'Dados salvos');
      cy.contains('button[type="submit"]', 'Salvar alterações').should('be.disabled');
    });

    it('troca o CPF só quando a pessoa pede e digita o número inteiro', () => {
      stubResidentDetail();
      stubResidentUpdated(CURRENT_RESIDENT, {});
      cy.visit(editResidentPath(CURRENT_RESIDENT.id));

      cy.get('[data-testid="change-cpf"]').click();
      typeInto('cpf', '52998224725');
      cy.contains('button[type="submit"]', 'Salvar alterações').click();

      cy.wait('@updateResident').its('request.body.cpf').should('eq', '52998224725');
    });

    it('versão velha mostra o aviso e "Recarregar dados" busca de novo', () => {
      stubResidentDetail();
      stubResidentUpdateRejected(CURRENT_RESIDENT, 409, 'concurrency.conflict');
      cy.visit(editResidentPath(CURRENT_RESIDENT.id));

      typeInto('profession', 'Médica');
      cy.contains('button[type="submit"]', 'Salvar alterações').click();

      cy.get('[data-testid="conflict"]').should('contain.text', 'alterados em outro lugar');
      cy.get('[data-testid="reload"]').click();
      cy.get('[data-testid="conflict"]').should('not.exist');
      cy.get('[formControlName="profession"]').should('have.value', 'Engenheira');
    });
  });

  describe('no celular', () => {
    it('o cadastro cabe na tela, sem rolagem horizontal', () => {
      cy.viewport(PHONE.width, PHONE.height);
      cy.visit(NEW_RESIDENT_PATH);

      cy.get('[formControlName="fullName"]').should('be.visible');
      cy.document().should((doc) => {
        expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
      });
    });

    it('a edição cabe na tela, sem rolagem horizontal', () => {
      stubResidentDetail();
      cy.viewport(PHONE.width, PHONE.height);
      cy.visit(editResidentPath(CURRENT_RESIDENT.id));

      cy.get('[formControlName="fullName"]').should('be.visible');
      cy.document().should((doc) => {
        expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
      });
    });
  });
});
