import { NEW_MANAGER, stubPostalCodeFailure } from '../support/condominio';
import { stubLogin, stubSignedIn } from '../support/session';

const FORM_PATH = '/condominio/cadastrar';

function field(section: string, name: string): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get(`[data-testid="${section}-section"] [formControlName="${name}"]`);
}

function type(section: string, name: string, text: string): void {
  field(section, name).click('right').type(text);
}

describe('Rascunho do cadastro do condomínio', () => {
  beforeEach(() => {
    stubPostalCodeFailure(404, 'condominium.postal_code.not_found');
    stubSignedIn(NEW_MANAGER);
  });

  it('recupera o que foi preenchido depois de recarregar a pagina, sem CPF nem data de nascimento', () => {
    cy.visit(FORM_PATH);

    type('manager', 'fullName', 'Marina Souza');
    type('manager', 'cpf', '52998224725');
    type('manager', 'birthDate', '15061980');
    type('condominium', 'name', 'Residencial das Flores');
    cy.window().should((win) => {
      const keys = Object.keys(win.localStorage).filter((key) =>
        key.includes('registration-draft'),
      );
      expect(keys).to.have.length(1);
      const stored = win.localStorage.getItem(keys[0])!;
      expect(stored).to.contain('Residencial das Flores');
      expect(stored).not.to.contain('52998224725');
      expect(stored).not.to.contain('15061980');
    });

    cy.reload();

    field('manager', 'fullName').should('have.value', 'Marina Souza');
    field('condominium', 'name').should('have.value', 'Residencial das Flores');
    field('manager', 'cpf').should('have.value', '');
    field('manager', 'birthDate').should('have.value', '');
    cy.get('[data-testid="draft-restored"]').should(
      'contain.text',
      'Recuperamos o que você já tinha preenchido',
    );
  });

  it('descartar limpa o formulario e o rascunho, e ele nao volta ao recarregar', () => {
    cy.visit(FORM_PATH);
    type('manager', 'fullName', 'Marina Souza');
    cy.window().should((win) => {
      expect(Object.keys(win.localStorage).some((key) => key.includes('registration-draft'))).to.eq(
        true,
      );
    });
    cy.reload();

    cy.get('[data-testid="discard-draft"]').click();

    cy.get('[data-testid="draft-restored"]').should('not.exist');
    field('manager', 'fullName').should('have.value', '');
    cy.reload();
    field('manager', 'fullName').should('have.value', '');
    cy.get('[data-testid="draft-restored"]').should('not.exist');
  });

  it('sair da conta apaga o rascunho: quem entra depois encontra o formulario limpo', () => {
    stubLogin(NEW_MANAGER);
    cy.visit(FORM_PATH);
    type('manager', 'fullName', 'Marina Souza');
    cy.window().should((win) => {
      expect(Object.keys(win.localStorage).some((key) => key.includes('registration-draft'))).to.eq(
        true,
      );
    });

    cy.get('[data-testid="sign-out"]').click();
    cy.location('pathname').should('eq', '/entrar');
    cy.window().should((win) => {
      expect(Object.keys(win.localStorage).some((key) => key.includes('registration-draft'))).to.eq(
        false,
      );
    });
    cy.get('input[type="email"]').type(NEW_MANAGER.email);
    cy.get('input[autocomplete="current-password"]').type('Senha-segura-123');
    cy.contains('button', 'Entrar').click();

    cy.location('pathname').should('eq', FORM_PATH);
    field('manager', 'fullName').should('have.value', '');
    cy.get('[data-testid="draft-restored"]').should('not.exist');
  });
});
