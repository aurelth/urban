import { chooseMenu } from '../support/menu';
import { DESKTOP, PHONE } from '../support/navegacao';
import { MANAGER, stubSignedIn } from '../support/session';
import { stubUnits } from '../support/unidades';
import {
  CURRENT_RESIDENT,
  MINOR_BIRTH_YEAR,
  MINOR_RESIDENT,
  NEW_RESIDENT_PATH,
  RESIDENTS_PATH,
  editResidentPath,
  fillMinorTexts,
  fillResidentTexts,
  stubResidentCreateRejected,
  stubInvitation,
  stubInvitationRejected,
  stubResident,
  stubResidentCreated,
  stubResidentDetail,
  stubResidentDetailInvitedLater,
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

    it('cadastra um dependente menor de idade sem profissão nem contato de emergência', () => {
      stubResidentCreated(MINOR_RESIDENT);
      cy.visit(NEW_RESIDENT_PATH);

      fillMinorTexts();
      cy.get('[data-testid="minor-note"]').should(
        'contain.text',
        'Para menores de 18 anos, profissão e contato de emergência são opcionais',
      );
      chooseMenu('[formControlName="unitId"]', 'Bloco A · 101');
      chooseMenu('[formControlName="kind"]', 'Dependente');
      cy.contains('button[type="submit"]', 'Cadastrar morador').click();

      cy.wait('@createResident')
        .its('request.body')
        .should('deep.equal', {
          fullName: 'Pedro Souza',
          cpf: '52998224725',
          birthDate: `${MINOR_BIRTH_YEAR}-04-10`,
          phone: '47998765432',
          email: null,
          profession: null,
          emergencyContact: null,
          unitId: '0199aaaa-0000-7000-8000-000000000001',
          kind: 'dependent',
          startDate: '2024-03-01',
        });
      cy.get('[data-testid="created"]').should('contain.text', 'Pedro Souza');
    });

    it('não deixa o menor de idade ser inquilino: avisa e não chama a API', () => {
      stubResidentCreated(MINOR_RESIDENT);
      cy.visit(NEW_RESIDENT_PATH);

      fillMinorTexts();
      chooseMenu('[formControlName="unitId"]', 'Bloco A · 101');
      chooseMenu('[formControlName="kind"]', 'Inquilino');
      cy.contains('button[type="submit"]', 'Cadastrar morador').click();

      cy.contains('mat-error', 'Quem tem menos de 18 anos só pode ser dependente').should(
        'be.visible',
      );
      cy.get('@createResident.all').should('have.length', 0);
    });

    it('o adulto continua precisando de profissão e contato, mesmo como dependente', () => {
      stubResidentCreated();
      cy.visit(NEW_RESIDENT_PATH);

      typeInto('fullName', 'Maria Souza');
      typeInto('cpf', '52998224725');
      typeInto('birthDate', '20051990');
      typeInto('phone', '47998765432');
      typeInto('startDate', '01032024');
      chooseMenu('[formControlName="unitId"]', 'Bloco A · 101');
      chooseMenu('[formControlName="kind"]', 'Dependente');
      cy.contains('button[type="submit"]', 'Cadastrar morador').click();

      cy.contains('mat-error', 'Informe a profissão').should('be.visible');
      cy.contains('mat-error', 'Informe o nome do contato').should('be.visible');
      cy.get('@createResident.all').should('have.length', 0);
    });

    it('mostra o menor de idade já cadastrado, sem profissão e contato, e salva sem exigi-los', () => {
      stubResidentDetail(MINOR_RESIDENT);
      stubResidentUpdated(MINOR_RESIDENT, { phone: '47991112222' });
      cy.visit(editResidentPath(MINOR_RESIDENT.id));

      cy.get('[formControlName="profession"]').should('have.value', '');
      cy.get('[data-testid="minor-note"]').should('be.visible');
      typeInto('phone', '47991112222');
      cy.contains('button[type="submit"]', 'Salvar alterações').click();

      cy.wait('@updateResident').its('request.body').should('include', {
        phone: '47991112222',
        profession: null,
        emergencyContact: null,
      });
      cy.get('[data-testid="saved"]').should('contain.text', 'Dados salvos');
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

  describe('acesso ao sistema', () => {
    it('convida pela edição: o cartão passa a "Convite enviado" e o salvamento seguinte usa a versão nova', () => {
      stubResidentDetailInvitedLater(CURRENT_RESIDENT);
      stubInvitation(CURRENT_RESIDENT);
      stubResidentUpdated({ ...CURRENT_RESIDENT, version: '8' }, { profession: 'Médica' });
      cy.visit(editResidentPath(CURRENT_RESIDENT.id));

      cy.get('[data-testid="access-card"]').should('contain.text', 'Acesso ao sistema');
      cy.get('[data-testid="invite"]').click();

      cy.wait('@invite');
      cy.get('[data-testid="access-status"]').should('contain.text', 'Convite enviado');
      cy.get('[data-testid="access-notice"]').should(
        'contain.text',
        'Convite enviado para ana@exemplo.com',
      );
      cy.get('[data-testid="resend-invite"]').should('be.visible');

      typeInto('profession', 'Médica');
      cy.contains('button[type="submit"]', 'Salvar alterações').click();
      cy.wait('@updateResident').its('request.body.version').should('eq', '8');
    });

    it('com alteração por salvar, pede para salvar antes de convidar', () => {
      stubResidentDetail();
      cy.visit(editResidentPath(CURRENT_RESIDENT.id));

      typeInto('profession', 'Médica');

      cy.get('[data-testid="access-blocked"]').should(
        'contain.text',
        'Salve ou descarte as alterações antes de convidar',
      );
      cy.get('[data-testid="invite"]').should('be.disabled');
    });

    it('mostra o motivo quando a API recusa o convite', () => {
      stubResidentDetail();
      stubInvitationRejected(CURRENT_RESIDENT, 409, 'identity.resident_access.email_unavailable');
      cy.visit(editResidentPath(CURRENT_RESIDENT.id));

      cy.get('[data-testid="invite"]').click();

      cy.get('[data-testid="access-error"]').should('contain.text', 'já tem uma conta');
      cy.get('[data-testid="access-status"]').should('contain.text', 'Sem acesso');
    });

    it('com acesso ativo, trava o e-mail e explica por quê', () => {
      stubResidentDetail(stubResident({ accessStatus: 'active' }));
      cy.visit(editResidentPath(CURRENT_RESIDENT.id));

      cy.get('[data-testid="access-status"]').should('contain.text', 'Acesso ativo');
      cy.get('[formControlName="email"]').should('be.disabled');
      cy.contains('mat-hint', 'não pode ser alterado depois que o morador cria a senha').should(
        'be.visible',
      );
      cy.get('[data-testid="invite"], [data-testid="resend-invite"]').should('not.exist');
    });

    it('com convite enviado, ainda deixa corrigir o e-mail e oferece reenviar', () => {
      stubResidentDetail(stubResident({ accessStatus: 'invited' }));
      stubResidentUpdated(stubResident({ accessStatus: 'invited' }), {
        email: 'ana.certa@exemplo.com',
      });
      cy.visit(editResidentPath(CURRENT_RESIDENT.id));

      cy.get('[formControlName="email"]').should('not.be.disabled');
      typeInto('email', 'ana.certa@exemplo.com');
      cy.contains('button[type="submit"]', 'Salvar alterações').click();

      cy.wait('@updateResident').its('request.body.email').should('eq', 'ana.certa@exemplo.com');
      cy.get('[data-testid="resend-invite"]').should('be.visible');
    });

    it('e-mail travado na API aparece como aviso do formulário', () => {
      stubResidentDetail(stubResident({ accessStatus: 'invited' }));
      stubResidentUpdateRejected(
        stubResident({ accessStatus: 'invited' }),
        409,
        'condominium.resident.email_locked',
      );
      cy.visit(editResidentPath(CURRENT_RESIDENT.id));

      typeInto('email', 'outro@exemplo.com');
      cy.contains('button[type="submit"]', 'Salvar alterações').click();

      cy.get('[role="alert"]').should('contain.text', 'não pode ser alterado');
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

    it('o cadastro de um menor de idade, com o aviso, cabe na tela sem rolagem horizontal', () => {
      cy.viewport(PHONE.width, PHONE.height);
      cy.visit(NEW_RESIDENT_PATH);

      typeInto('birthDate', `1004${MINOR_BIRTH_YEAR}`);
      cy.get('[data-testid="minor-note"]').should('be.visible');
      cy.document().should((doc) => {
        expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
      });
    });

    it('a edição cabe na tela, sem rolagem horizontal, com o cartão de acesso', () => {
      stubResidentDetail();
      cy.viewport(PHONE.width, PHONE.height);
      cy.visit(editResidentPath(CURRENT_RESIDENT.id));

      cy.get('[formControlName="fullName"]').should('be.visible');
      cy.get('[data-testid="access-card"]').scrollIntoView().should('be.visible');
      cy.document().should((doc) => {
        expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
      });
    });
  });
});
