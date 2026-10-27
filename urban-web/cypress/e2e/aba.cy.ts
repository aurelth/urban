import { stubAnonymous, stubSignedIn } from '../support/session';

const ICONES: ReadonlyArray<readonly [string, string]> = [
  ['/favicon.svg', 'image/svg+xml'],
  ['/favicon.ico', 'image/'],
  ['/apple-touch-icon.png', 'image/png'],
];

describe('Aba do navegador', () => {
  beforeEach(() => {
    stubAnonymous();
  });

  it('mostra o nome da aplicação e o da tela no título da aba', () => {
    cy.visit('/entrar');

    cy.title().should('eq', 'Urban - Gestão condominial | Entrar');
  });

  it('mantém o nome da aplicação no título do painel', () => {
    stubSignedIn();
    cy.visit('/dashboard');

    cy.title().should('eq', 'Urban - Gestão condominial | Painel');
  });

  it('declara o favicon em SVG, o de reserva em ICO e o ícone do iPhone', () => {
    cy.visit('/entrar');

    cy.get('head link[rel="icon"][href="favicon.svg"]').should(
      'have.attr',
      'type',
      'image/svg+xml',
    );
    cy.get('head link[rel="icon"][href="favicon.ico"]').should('have.attr', 'sizes', '32x32');
    cy.get('head link[rel="apple-touch-icon"]').should('have.attr', 'href', 'apple-touch-icon.png');
  });

  ICONES.forEach(([path, type]) => {
    it(`serve ${path} como imagem, não como a página do app`, () => {
      cy.request(path).its('headers.content-type').should('include', type);
    });
  });
});
