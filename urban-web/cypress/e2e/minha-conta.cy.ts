import { PALETTE, expectErrorsToStayInTheirOwnSpace } from '../support/layout';
import { stubChangeEmail, stubChangePassword, stubConfirmEmailChange } from '../support/perfil';
import { MANAGER, stubAnonymous, stubLogin, stubSignedIn } from '../support/session';

const ACCOUNT_PATH = '/conta';

function passwordSection(): Cypress.Chainable<JQuery<HTMLElement>> {
  return cy.get('[data-testid="password-section"]');
}

function fillPasswordForm(current: string, next: string, confirm = next): void {
  // O clique à direita sai do rótulo flutuante, que nos nomes longos chega ao centro do campo vazio.
  passwordSection().find('[formControlName="currentPassword"]').click('right').type(current);
  passwordSection().find('[formControlName="newPassword"]').click('right').type(next);
  passwordSection().find('[formControlName="confirmPassword"]').click('right').type(confirm);
}

describe('Minha conta', () => {
  beforeEach(() => {
    stubSignedIn();
  });

  it('mostra "Alterar e-mail" como botão principal: fundo índigo preenchido e texto branco', () => {
    cy.visit(ACCOUNT_PATH, {
      onBeforeLoad: (win) => win.localStorage.setItem('urban.theme', 'light'),
    });

    cy.get('[data-testid="change-email"]')
      .should('have.css', 'background-color', PALETTE.light.primary)
      .and('have.css', 'color', PALETTE.light.onPrimary);
    // Igual ao botão "Alterar senha", o outro botão principal da tela.
    cy.get('[data-testid="password-section"] button[type="submit"]')
      .should('have.css', 'background-color', PALETTE.light.primary)
      .and('have.css', 'color', PALETTE.light.onPrimary);
  });

  it('abre pelo link do cabecalho e mostra o e-mail atual', () => {
    cy.visit('/dashboard');
    cy.get('[data-testid="nav-account"]').click();

    cy.location('pathname').should('eq', ACCOUNT_PATH);
    cy.contains('h1', 'Minha conta').should('be.visible');
    cy.get('[data-testid="current-email"]').should('have.text', MANAGER.email);
  });

  describe('trocar a senha', () => {
    it('troca a senha, encerra a sessao e leva a entrada com o aviso em verde', () => {
      stubChangePassword();
      cy.visit(ACCOUNT_PATH);

      fillPasswordForm('Senha-antiga-123', 'Senha-nova-456');
      cy.contains('button', 'Alterar senha').click();

      cy.wait('@changePassword').its('request.body').should('deep.equal', {
        currentPassword: 'Senha-antiga-123',
        newPassword: 'Senha-nova-456',
      });
      cy.location('pathname').should('eq', '/entrar');
      cy.location('search').should('contain', 'motivo=senha-alterada');
      cy.get('[data-testid="password-changed"]')
        .should('contain.text', 'Senha alterada. Entre com a nova senha')
        .find('.text-success mat-icon')
        .should('have.css', 'color', PALETTE.light.success);
    });

    it('depois de trocar, a pessoa entra com a senha nova', () => {
      stubChangePassword();
      stubLogin();
      cy.visit(ACCOUNT_PATH);
      fillPasswordForm('Senha-antiga-123', 'Senha-nova-456');
      cy.contains('button', 'Alterar senha').click();
      cy.location('pathname').should('eq', '/entrar');
      stubAnonymous();

      cy.get('input[type="email"]').type(MANAGER.email);
      cy.get('input[autocomplete="current-password"]').type('Senha-nova-456');
      cy.contains('button', 'Entrar').click();

      cy.wait('@login').its('request.body.password').should('eq', 'Senha-nova-456');
    });

    it('mostra a senha atual incorreta e continua logado', () => {
      stubChangePassword(403, 'identity.password.incorrect');
      cy.visit(ACCOUNT_PATH);

      fillPasswordForm('Senha-errada-999', 'Senha-nova-456');
      cy.contains('button', 'Alterar senha').click();

      cy.wait('@changePassword');
      passwordSection().find('[role="alert"]').should('contain.text', 'Senha atual incorreta');
      cy.location('pathname').should('eq', ACCOUNT_PATH);
      cy.get('[data-testid="current-user"]').should('be.visible');
    });

    it('nao envia quando a confirmacao e diferente ou a senha e fraca', () => {
      stubChangePassword();
      cy.visit(ACCOUNT_PATH);

      fillPasswordForm('Senha-antiga-123', 'curta1', 'curta2');
      cy.contains('button', 'Alterar senha').click();

      cy.contains('mat-error', 'A senha precisa ter pelo menos 10 caracteres').should('be.visible');
      cy.contains('mat-error', 'As senhas não são iguais').should('be.visible');
      cy.get('@changePassword.all').should('have.length', 0);
    });

    it('mantem os avisos longe dos botoes em uma janela estreita', () => {
      cy.viewport(375, 667);
      cy.visit(ACCOUNT_PATH);

      fillPasswordForm('Senha-antiga-123', 'curta1', 'curta2');
      cy.contains('button', 'Alterar senha').click();

      cy.contains('mat-error', 'As senhas não são iguais').should('be.visible');
      expectErrorsToStayInTheirOwnSpace();
      cy.document().should((doc) => {
        expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
      });
    });
  });

  describe('trocar o e-mail', () => {
    function openDialog(): void {
      cy.get('[data-testid="change-email"]').click();
      // O elemento do diálogo é `display: contents`, então a visibilidade se confere num filho.
      cy.get('urban-change-email-dialog h2').should('be.visible');
    }

    it('pede o novo e-mail e a senha, e avisa em verde que o link foi pedido', () => {
      stubChangeEmail();
      cy.visit(ACCOUNT_PATH);

      openDialog();
      cy.get('urban-change-email-dialog input[type="email"]').type('nova@example.com');
      cy.get('urban-change-email-dialog input[autocomplete="current-password"]').type(
        'Senha-segura-123',
      );
      cy.get('urban-change-email-dialog').contains('button', 'Enviar link de confirmação').click();

      cy.wait('@changeEmail').its('request.body').should('deep.equal', {
        password: 'Senha-segura-123',
        newEmail: 'nova@example.com',
      });
      cy.get('urban-change-email-dialog').should('not.exist');
      cy.get('[data-testid="email-requested"]')
        .should('contain.text', 'nova@example.com')
        .find('.text-success mat-icon')
        .should('have.css', 'color', PALETTE.light.success);
      cy.get('[data-testid="current-email"]').should('have.text', MANAGER.email);
    });

    it('mostra a senha incorreta dentro do dialogo', () => {
      stubChangeEmail(403, 'identity.password.incorrect');
      cy.visit(ACCOUNT_PATH);

      openDialog();
      cy.get('urban-change-email-dialog input[type="email"]').type('nova@example.com');
      cy.get('urban-change-email-dialog input[autocomplete="current-password"]').type(
        'Senha-errada-999',
      );
      cy.get('urban-change-email-dialog').contains('button', 'Enviar link de confirmação').click();

      cy.get('urban-change-email-dialog [role="alert"]').should('contain.text', 'Senha incorreta');
      cy.get('[data-testid="email-requested"]').should('not.exist');
    });

    it('cabe na tela do celular, com o dialogo aberto', () => {
      cy.viewport(375, 667);
      cy.visit(ACCOUNT_PATH);

      openDialog();

      cy.document().should((doc) => {
        expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
      });
    });
  });
});

describe('Confirmar a troca de e-mail pelo link', () => {
  beforeEach(() => {
    stubAnonymous();
  });

  it('confirma sozinho ao abrir o link e leva a entrar com o e-mail novo', () => {
    stubConfirmEmailChange();
    cy.visit('/confirmar-troca-de-email?token=token-do-link');

    cy.wait('@confirmEmailChange')
      .its('request.body')
      .should('deep.equal', { token: 'token-do-link' });
    cy.contains('h1', 'E-mail alterado')
      .should('be.visible')
      .and('have.css', 'color', PALETTE.light.success);
    cy.contains('a', 'Entrar').click();
    cy.location('pathname').should('eq', '/entrar');
  });

  it('explica o link que nao serve mais', () => {
    stubConfirmEmailChange(400, 'identity.email_change.expired');
    cy.visit('/confirmar-troca-de-email?token=token-do-link');

    cy.contains('h1', 'Link inválido').should('be.visible');
    cy.get('[role="alert"]').should('contain.text', 'Link expirado');
  });

  it('avisa quando o link veio sem o codigo', () => {
    stubConfirmEmailChange();
    cy.visit('/confirmar-troca-de-email');

    cy.contains('h1', 'Link incompleto').should('be.visible');
    cy.get('@confirmEmailChange.all').should('have.length', 0);
  });

  it('cabe na tela do celular, sem rolagem horizontal', () => {
    cy.viewport(375, 667);
    stubConfirmEmailChange();
    cy.visit('/confirmar-troca-de-email?token=token-do-link');

    cy.contains('h1', 'E-mail alterado').should('be.visible');
    cy.document().should((doc) => {
      expect(doc.documentElement.scrollWidth).to.be.at.most(doc.documentElement.clientWidth);
    });
  });
});
