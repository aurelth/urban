interface DomNode {
  nodeId: number;
}

interface RemoteObject {
  result: { objectId: string };
}

function send<T = unknown>(command: string, params: object = {}): Promise<T> {
  return Cypress.automation('remote:debugger:protocol', { command, params }) as Promise<T>;
}

/**
 * Faz o navegador tratar o campo como preenchido automaticamente (a pseudo-classe `:autofill`),
 * que é o estado em que o Chrome pinta o fundo azulado. Só funciona em navegadores baseados em Chromium.
 */
export function forceAutofill(selector: string): void {
  cy.get(selector).should('be.visible');
  cy.then(async () => {
    await send('DOM.enable');
    await send('CSS.enable');
    await send('DOM.getDocument');
    const { result } = await send<RemoteObject>('Runtime.evaluate', {
      expression: `document.querySelector('.aut-iframe').contentDocument.querySelector(${JSON.stringify(selector)})`,
    });
    const { nodeId } = await send<DomNode>('DOM.requestNode', { objectId: result.objectId });
    await send('CSS.forcePseudoState', { nodeId, forcedPseudoClasses: ['autofill'] });
  });
}

export function backgroundAlpha($element: JQuery<HTMLElement>): number {
  const match = /rgba?\((?:[^,]+,){2}[^,)]+(?:,\s*([\d.]+))?\)/.exec(
    getComputedStyle($element[0]).backgroundColor,
  );
  return match?.[1] === undefined ? 1 : Number(match[1]);
}
