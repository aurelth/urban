import { MANAGERS_PATH, OVERVIEW_PATH, stubManagers } from '../support/administracao';
import {
  CONTACT_PATH,
  MESSAGES_PATH,
  stubContactMessages,
  stubContactSend,
} from '../support/contato';
import { stubTerms } from '../support/cadastro';
import { NEW_MANAGER } from '../support/condominio';
import { expectBreathingRoom, expectDialogBreathingRoom } from '../support/espacamento';
import {
  CLOSED_MONTH,
  ENTRIES_PATH,
  EXPENSES_PATH,
  FILLED_MONTH,
  OPENED_MONTH,
  stubExpenses,
  stubFinanceRejected,
  stubMonth,
} from '../support/financeiro';
import { READINGS_PATH, stubReadings } from '../support/leituras';
import { PROFILE_PATH, stubProfile } from '../support/perfil';
import { ADMIN, REQUESTS_PATH, stubRequests } from '../support/pedidos';
import { MANAGER, stubAnonymous, stubSignedIn } from '../support/session';
import { TARIFFS_PATH, stubTariffs } from '../support/tarifas';
import {
  CURRENT_RESIDENT,
  NEW_RESIDENT_PATH,
  RESIDENTS_PATH,
  editResidentPath,
  stubInvitation,
  stubInvitationRejected,
  stubResidentDetail,
  stubResidents,
} from '../support/moradores';
import { BATCH_PATH, UNITS_PATH, stubUnits } from '../support/unidades';

function closeDialog(): void {
  cy.get('.mat-mdc-dialog-container [data-testid="cancel"]').click();
  cy.get('.mat-mdc-dialog-container').should('not.exist');
}

const SCREENS: readonly (readonly [string, number, number])[] = [
  ['no computador', 1280, 800],
  ['no celular', 375, 700],
];

SCREENS.forEach(([where, width, height]) => {
  describe(`Espaço entre textos e containers ${where}`, () => {
    beforeEach(() => {
      cy.viewport(width, height);
    });

    ['/entrar', '/cadastrar', '/esqueci-a-senha', '/termos', '/privacidade'].forEach((path) => {
      it(`não cola texto em container em ${path}`, () => {
        stubAnonymous();
        cy.visit(path);

        cy.get('h1').should('be.visible');
        expectBreathingRoom();
      });
    });

    it('não cola texto em container na criação de senha', () => {
      stubAnonymous();
      stubTerms();
      cy.visit('/confirmar-email?token=token-do-link');

      cy.get('input[autocomplete="new-password"]').should('be.visible');
      expectBreathingRoom();
    });

    it('não cola texto em container no painel, em minha conta e nos dados do condomínio', () => {
      stubSignedIn(MANAGER);
      stubProfile();
      cy.visit('/dashboard');
      cy.get('mat-card.module').should('have.length', 3);
      expectBreathingRoom();
      cy.visit('/conta');
      cy.get('[data-testid="password-section"]').should('be.visible');
      expectBreathingRoom();
      cy.visit(PROFILE_PATH);
      cy.get('[data-testid="condominium-section"]').should('be.visible');
      expectBreathingRoom();
    });

    it('não cola texto em container no cadastro do condomínio', () => {
      stubSignedIn(NEW_MANAGER);
      cy.visit('/condominio/cadastrar');

      cy.get('[data-testid="condominium-section"]').should('be.visible');
      expectBreathingRoom();
    });

    it('não cola texto em container nos pedidos de cadastro, com e sem a confirmação de recusa', () => {
      stubSignedIn(ADMIN);
      stubRequests();
      cy.visit(REQUESTS_PATH);
      cy.get('[data-testid="request"]').should('have.length', 2);
      expectBreathingRoom();
      cy.get('[data-testid="reject"]').first().click();
      cy.get('[data-testid="reject-confirmation"]').should('be.visible');
      expectBreathingRoom();
    });

    it('não cola texto em container no contato, antes e depois do envio', () => {
      stubSignedIn(MANAGER);
      stubContactSend();
      cy.visit(CONTACT_PATH);
      cy.get('textarea[formControlName="body"]').should('be.visible');
      expectBreathingRoom();
      cy.get('input[formControlName="subject"]').click('right').type('Corrigir o CNPJ');
      cy.get('textarea[formControlName="body"]').click('right').type('Digitei o CNPJ errado.');
      cy.contains('button', 'Enviar mensagem').click();
      cy.get('[data-testid="sent"]').should('be.visible');
      expectBreathingRoom();
    });

    it('não cola texto em container nas mensagens do administrador', () => {
      stubSignedIn(ADMIN);
      stubContactMessages();
      cy.visit(MESSAGES_PATH);
      cy.get('[data-testid="message"]').should('have.length', 2);
      expectBreathingRoom();
    });

    it('não cola texto em container no formulário de resposta e na resposta enviada', () => {
      stubSignedIn(ADMIN);
      stubContactMessages();
      cy.visit(MESSAGES_PATH);
      cy.get('[data-testid="reply"]').first().click();
      cy.get('textarea[formControlName="body"]').click('right').type('Corrigi o CNPJ no cadastro.');
      cy.get('[data-testid="file-input"]').selectFile(
        { contents: Cypress.Buffer.from('texto'), fileName: 'ata.txt' },
        { force: true },
      );
      cy.get('[data-testid="attachment-name"]').should('be.visible');
      expectBreathingRoom();
      cy.contains('button', 'Enviar resposta').click();
      cy.get('[data-testid="reply-item"]').should('be.visible');
      expectBreathingRoom();
    });

    it('não cola texto em container na visão geral, nos síndicos e no diálogo de suspensão', () => {
      stubSignedIn(ADMIN);
      stubManagers();
      cy.visit(OVERVIEW_PATH);
      cy.get('[data-testid="stat-active"]').should('be.visible');
      expectBreathingRoom();
      cy.visit(MANAGERS_PATH);
      cy.get('[data-testid="managers-table"]').should('be.visible');
      expectBreathingRoom();
      cy.get('button[aria-label="Suspender Ana"]').click();
      expectDialogBreathingRoom();
    });

    it('não cola texto em container nas unidades, no lote e no diálogo de unidade', () => {
      stubSignedIn(MANAGER);
      stubUnits();
      cy.visit(UNITS_PATH);
      cy.get('[data-testid="units-table"]').should('be.visible');
      expectBreathingRoom();
      cy.get('[data-testid="new-unit"]').click();
      expectDialogBreathingRoom();
      closeDialog();
      cy.visit(BATCH_PATH);
      cy.get('[data-testid="generator"]').should('be.visible');
      expectBreathingRoom();
    });

    it('não cola texto em container na lista de moradores', () => {
      stubSignedIn(MANAGER);
      stubResidents();
      cy.visit(RESIDENTS_PATH);
      cy.get('[data-testid="residents-table"]').should('be.visible');
      expectBreathingRoom();
    });

    it('não cola texto em container no cadastro de morador', () => {
      stubSignedIn(MANAGER);
      stubUnits();
      cy.visit(NEW_RESIDENT_PATH);
      cy.get('[formControlName="fullName"]').should('be.visible');
      expectBreathingRoom();
    });

    it('não cola texto em container na edição de morador', () => {
      stubSignedIn(MANAGER);
      stubResidentDetail();
      cy.visit(editResidentPath(CURRENT_RESIDENT.id));
      cy.get('[formControlName="fullName"]').should('be.visible');
      expectBreathingRoom();
    });

    it('não cola texto em container no cartão de acesso do morador, com o aviso do convite e com o erro', () => {
      stubSignedIn(MANAGER);
      stubResidentDetail();
      stubInvitation(CURRENT_RESIDENT);
      cy.visit(editResidentPath(CURRENT_RESIDENT.id));
      cy.get('[data-testid="access-card"]').should('be.visible');
      expectBreathingRoom();
      cy.get('[data-testid="invite"]').click();
      cy.get('[data-testid="access-notice"]').should('be.visible');
      expectBreathingRoom();
    });

    it('não cola texto em container na lista de moradores com convite, situação e erro de convite', () => {
      stubSignedIn(MANAGER);
      stubResidents();
      stubInvitationRejected(CURRENT_RESIDENT, 409, 'identity.resident_access.email_unavailable');
      cy.visit(RESIDENTS_PATH);
      cy.get('[data-testid="invite"]').first().click();
      cy.get('[data-testid="access-error"]').should('be.visible');
      expectBreathingRoom();
    });

    it('não cola texto em container nos vínculos do morador, com os formulários de saída e de nova unidade', () => {
      stubSignedIn(MANAGER);
      stubUnits();
      stubResidentDetail();
      cy.visit(editResidentPath(CURRENT_RESIDENT.id));
      cy.get('[data-testid="occupancies"]').should('be.visible');
      expectBreathingRoom();
      cy.get('[data-testid="end-occupancy"]').click();
      cy.get('[data-testid="end-form"]').should('be.visible');
      expectBreathingRoom();
      cy.get('[data-testid="add-occupancy"]').click();
      cy.get('[data-testid="add-form"]').should('be.visible');
      expectBreathingRoom();
    });

    it('não cola texto em container nas despesas e nos diálogos de despesa e de grupo', () => {
      stubSignedIn(MANAGER);
      stubExpenses();
      cy.visit(EXPENSES_PATH);
      cy.get('[data-testid="expense-row"]').should('exist');
      expectBreathingRoom();
      cy.get('[data-testid="new-expense"]').click();
      expectDialogBreathingRoom();
      closeDialog();
      cy.get('[data-testid="new-group"]').click();
      expectDialogBreathingRoom();
      closeDialog();
      cy.get('button[aria-label="Alterar parcela de Elevador"]').click();
      expectDialogBreathingRoom();
      closeDialog();
      cy.get('button[aria-label="Encerrar Conta de luz"]').click();
      expectDialogBreathingRoom();
    });

    it('não cola texto em container no diálogo de informar o valor de um lançamento', () => {
      stubSignedIn(MANAGER);
      stubMonth(OPENED_MONTH);
      cy.visit(ENTRIES_PATH);
      cy.get('button[aria-label="Informar valor de Conta de luz"]').click();

      expectDialogBreathingRoom();
    });

    it('não cola texto em container no diálogo de alterar o e-mail', () => {
      stubSignedIn(MANAGER);
      cy.visit('/conta');
      cy.get('[data-testid="change-email"]').click();

      expectDialogBreathingRoom();
    });

    it('não cola texto em container nos lançamentos e no diálogo de fechar o mês, com e sem erro', () => {
      stubSignedIn(MANAGER);
      stubMonth(FILLED_MONTH);
      stubFinanceRejected(
        'POST',
        /\/api\/finance\/months\/[\d-]+\/close$/,
        422,
        'condominium.apportionment_not_ready',
        'close',
        {
          reason: 'fractions_total',
          fractionTotal: 0.05,
        },
      );
      cy.visit(ENTRIES_PATH);
      cy.get('[data-testid="entry-row"]').should('exist');
      expectBreathingRoom();
      cy.get('[data-testid="close-month"]').click();
      expectDialogBreathingRoom();
      cy.get('urban-close-month-dialog').contains('button', 'Fechar mês').click();
      cy.wait('@close');
      cy.get('urban-close-month-dialog [data-testid="failure-link"]').should('be.visible');
      expectDialogBreathingRoom();
    });

    it('não cola texto em container no mês fechado e no diálogo de reabrir', () => {
      stubSignedIn(MANAGER);
      stubMonth(CLOSED_MONTH);
      cy.visit(ENTRIES_PATH);
      cy.get('[data-testid="closing-card"]').should('exist');
      expectBreathingRoom();
      cy.get('[data-testid="reopen-month"]').click();
      expectDialogBreathingRoom();
    });

    it('não cola texto em container nas tarifas e nos diálogos de tarifa e de fundo de reserva', () => {
      stubSignedIn(MANAGER);
      stubTariffs();
      cy.visit(TARIFFS_PATH);
      cy.get('[data-testid="tariff-item"]').should('exist');
      expectBreathingRoom();
      cy.get('[data-testid="new-tariff"]').click();
      expectDialogBreathingRoom();
      closeDialog();
      cy.get('[data-testid="edit-reserve-percent"]').click();
      expectDialogBreathingRoom();
    });

    it('não cola texto em container nas leituras do mês', () => {
      stubSignedIn(MANAGER);
      stubReadings();
      cy.visit(READINGS_PATH);
      cy.get('[data-testid="unit-row"]').should('exist');

      expectBreathingRoom();
    });
  });
});
