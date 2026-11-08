/**
 * Abre a lista suspensa `urban-menu-select` e clica na opção de texto `label`, como a pessoa faria. O painel é fixo:
 * sem `scrollBehavior: false` o Cypress rola a página e o menu sai de cima do campo; por isso a rolagem da lista, quando
 * ela é longa (as UFs), é feita só dentro do painel.
 */
export function chooseMenu(
  host: string | Cypress.Chainable<JQuery<HTMLElement>>,
  label: string,
): void {
  const element = typeof host === 'string' ? cy.get(host) : host;
  element.find('button').click();
  cy.contains('[role="menuitemradio"]', label)
    .then(($item) => {
      $item[0].scrollIntoView({ block: 'nearest' });
    })
    .click({ scrollBehavior: false });
}
