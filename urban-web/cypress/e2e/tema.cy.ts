import { stubAnonymous } from '../support/session';

interface Cenario {
  systemDark: boolean;
  saved?: 'light' | 'dark';
  storageBlocked?: boolean;
  withoutApp?: boolean;
}

function visitEntrar({ systemDark, saved, storageBlocked, withoutApp }: Cenario): void {
  stubAnonymous();
  if (withoutApp) {
    cy.intercept(
      { method: 'GET', url: /\.js(\?.*)?$/ },
      { body: '', headers: { 'content-type': 'text/javascript' } },
    );
  }
  cy.visit('/entrar', {
    onBeforeLoad: (win) => {
      const original = win.matchMedia.bind(win);
      cy.stub(win, 'matchMedia').callsFake((query: string) =>
        query.includes('prefers-color-scheme: dark')
          ? ({ matches: systemDark, media: query } as MediaQueryList)
          : original(query),
      );
      if (saved) {
        win.localStorage.setItem('urban.theme', saved);
      }
      if (storageBlocked) {
        cy.stub(win.Storage.prototype, 'getItem').throws(new Error('storage bloqueado'));
      }
    },
  });
}

describe('Tema na tela de entrada', () => {
  it('abre escuro quando o sistema está no escuro e não há escolha salva', () => {
    visitEntrar({ systemDark: true });

    cy.get('html').should('have.class', 'app-dark');
  });

  it('abre claro quando o sistema está no claro e não há escolha salva', () => {
    visitEntrar({ systemDark: false });

    cy.get('html').should('not.have.class', 'app-dark');
  });

  it('respeita a escolha clara salva mesmo com o sistema no escuro', () => {
    visitEntrar({ systemDark: true, saved: 'light' });

    cy.get('html').should('not.have.class', 'app-dark');
  });

  it('respeita a escolha escura salva mesmo com o sistema no claro', () => {
    visitEntrar({ systemDark: false, saved: 'dark' });

    cy.get('html').should('have.class', 'app-dark');
  });

  it('segue o sistema quando o navegador bloqueia o armazenamento', () => {
    visitEntrar({ systemDark: true, storageBlocked: true });

    cy.get('html').should('have.class', 'app-dark');
  });

  describe('antes de o Angular carregar (script do index.html)', () => {
    it('já abre escuro quando o sistema está no escuro', () => {
      visitEntrar({ systemDark: true, withoutApp: true });

      cy.get('html').should('have.class', 'app-dark');
    });

    it('já abre claro quando a escolha salva é clara, mesmo com o sistema no escuro', () => {
      visitEntrar({ systemDark: true, saved: 'light', withoutApp: true });

      cy.get('html').should('not.have.class', 'app-dark');
    });

    it('já abre escuro quando a escolha salva é escura, mesmo com o sistema no claro', () => {
      visitEntrar({ systemDark: false, saved: 'dark', withoutApp: true });

      cy.get('html').should('have.class', 'app-dark');
    });

    it('já segue o sistema quando o navegador bloqueia o armazenamento', () => {
      visitEntrar({ systemDark: true, storageBlocked: true, withoutApp: true });

      cy.get('html').should('have.class', 'app-dark');
    });
  });
});
