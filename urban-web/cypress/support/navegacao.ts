/** Largura em que o menu lateral fica fixo (breakpoint `lg` do Tailwind): abaixo dela ele vira uma gaveta. */
export const DESKTOP = { width: 1280, height: 800 } as const;
export const PHONE = { width: 375, height: 700 } as const;

/** Abre o menu do avatar, como a pessoa faria: clicando na foto (as iniciais) ao lado da saudação. */
export function openUserMenu(): void {
  cy.get('[data-testid="user-menu"]').click();
  cy.get('.mat-mdc-menu-panel').should('be.visible');
}

/** Abre o menu do avatar e escolhe um dos itens (`menu-profile`, `menu-account` ou `sign-out`). */
export function chooseUserMenu(testId: 'menu-profile' | 'menu-account' | 'sign-out'): void {
  openUserMenu();
  cy.get(`.mat-mdc-menu-panel [data-testid="${testId}"]`).click();
}

/** Sai da conta pelo menu do avatar. */
export function signOut(): void {
  chooseUserMenu('sign-out');
}

/** Abre a gaveta do menu lateral pelo botão do cabeçalho (só existe abaixo de 1024 px). */
export function openDrawer(): void {
  cy.get('[data-testid="menu-toggle"]').click();
  cy.get('mat-sidenav').should('have.class', 'mat-drawer-opened');
}

/**
 * Confere o texto de um elemento sem os espaços das pontas, que o Angular mantém entre a marcação e o texto.
 * Com `should` o Cypress repete a conferência até o texto assentar.
 */
export function expectText(selector: string, expected: string): void {
  cy.get(selector).should(($element) => {
    expect($element.text().trim()).to.eq(expected);
  });
}
