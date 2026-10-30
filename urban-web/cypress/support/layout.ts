/**
 * O que um aviso de erro nunca pode cobrir: caixas de outros campos, links, botões e caixas de seleção.
 * O Material reserva uma linha para o erro de cada campo; uma mensagem que quebra em duas, numa coluna
 * estreita, vazaria por cima do que vem abaixo.
 */
const COVERABLE = '.mdc-notched-outline, a[href], button, .mat-mdc-checkbox';

function isVisible(element: Element): boolean {
  return element.getClientRects().length > 0;
}

function textBox(element: Element): DOMRect {
  const range = element.ownerDocument.createRange();
  range.selectNodeContents(element);
  return range.getBoundingClientRect();
}

function overlaps(a: DOMRect, b: DOMRect): boolean {
  const tolerance = 1;
  return (
    a.left < b.right - tolerance &&
    a.right > b.left + tolerance &&
    a.top < b.bottom - tolerance &&
    a.bottom > b.top + tolerance
  );
}

function describeElement(element: Element): string {
  const name = element.closest('mat-form-field')?.querySelector('mat-label')?.textContent?.trim();
  return name
    ? `o campo "${name}"`
    : `<${element.tagName.toLowerCase()}> "${element.textContent?.trim()}"`;
}

/**
 * Confere, com as medidas reais da tela, que cada `mat-error` visível cabe na largura da janela e não
 * cobre nenhum campo (a não ser o dele), link, botão ou caixa de seleção.
 */
export function expectErrorsToStayInTheirOwnSpace(): void {
  cy.document().then((doc) => {
    const errors = [...doc.querySelectorAll('mat-error')].filter(isVisible);
    expect(errors.length, 'avisos de erro na tela').to.be.greaterThan(0);
    const targets = [...doc.querySelectorAll(COVERABLE)].filter(isVisible);
    const width = doc.documentElement.clientWidth;

    for (const error of errors) {
      const message = error.textContent?.trim();
      const box = textBox(error);
      expect(box.right, `"${message}" cabe na largura da janela`).to.be.at.most(width);

      const ownField = error.closest('mat-form-field');
      for (const target of targets) {
        if (ownField?.contains(target)) {
          continue;
        }
        expect(
          overlaps(box, target.getBoundingClientRect()),
          `"${message}" não cobre ${describeElement(target)}`,
        ).to.equal(false);
      }
    }
  });
}

/** Cor do papel "error" do tema atual, no formato que o navegador devolve (rgb(...)). */
export function themeErrorColor(): Cypress.Chainable<string> {
  return cy.document().then((doc) => {
    const probe = doc.createElement('span');
    probe.style.color = 'var(--mat-sys-error)';
    doc.body.append(probe);
    const color = doc.defaultView!.getComputedStyle(probe).color;
    probe.remove();
    return color;
  });
}
