describe('Painel', () => {
  it('abre o painel na rota inicial', () => {
    cy.visit('/');

    cy.location('pathname').should('eq', '/dashboard');
    cy.contains('h1', 'Urban').should('be.visible');
    cy.get('mat-card').should('have.length', 3);
  });

  it('alterna entre tema claro e escuro', () => {
    // Parte sempre do tema claro, independente da preferencia do sistema de quem roda o teste.
    cy.visit('/', {
      onBeforeLoad: (win) => win.localStorage.setItem('urban.theme', 'light'),
    });
    cy.get('html').should('not.have.class', 'app-dark');

    cy.get('button[aria-label="Usar tema escuro"]').click();
    cy.get('html').should('have.class', 'app-dark');

    cy.get('button[aria-label="Usar tema claro"]').click();
    cy.get('html').should('not.have.class', 'app-dark');
  });
});
