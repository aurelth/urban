/**
 * Mede o espaço entre cada texto e o container vizinho (card, tabela, campo ou botão com contorno) e acusa o que está colado.
 * O Tailwind zera as margens dos elementos, então sem espaço explícito o texto encosta no container de baixo ou de cima.
 */

/** Texto acima ou abaixo de um card ou de uma tabela: pelo menos 16 px até a borda. */
export const MIN_GAP = 16;
/** Texto acima de um campo: o rótulo flutuante fica sobre a borda e sobe uns 8 px, então o texto precisa de mais espaço. */
export const MIN_GAP_ABOVE_FIELD = 24;
/** Texto vizinho de um botão: um pouco menos, porque o botão é menor e a altura dele já inclui folga. */
export const MIN_GAP_BUTTON = 12;

/** Textos que não contam como "vizinhos": só para leitor de tela, ou o que fica dentro do campo (rótulo, valor, marcador). */
const IGNORED_TEXT =
  '.sr-only, .cdk-visually-hidden, .cdk-live-announcer-element, [aria-hidden="true"], .mat-mdc-text-field-wrapper, mat-icon';

/** Containers: cards e tabelas com borda, o contorno dos campos e os botões com contorno ou preenchidos. */
const CONTAINERS =
  'mat-card, .mat-mdc-card, .rounded-xl, .rounded-2xl, .mat-mdc-text-field-wrapper, .mdc-button--outlined, .mdc-button--unelevated';

interface Edge {
  readonly top: number;
  readonly bottom: number;
  readonly left: number;
  readonly right: number;
}

export interface Crowding {
  readonly side: 'acima' | 'abaixo';
  readonly gap: number;
  readonly min: number;
  readonly text: string;
  readonly container: string;
}

function isVisible(doc: Document, el: Element): boolean {
  const style = (doc.defaultView as Window).getComputedStyle(el);
  return style.display !== 'none' && style.visibility !== 'hidden';
}

function hasBorderOrFill(doc: Document, el: Element): boolean {
  const style = (doc.defaultView as Window).getComputedStyle(el);
  const alpha = (color: string): number => {
    const parts = /rgba?\(([^)]+)\)/.exec(color)?.[1].split(',').map(parseFloat) ?? [];
    return parts.length === 4 ? parts[3] : parts.length === 3 ? 1 : 0;
  };
  const border =
    parseFloat(style.borderTopWidth) > 0 &&
    style.borderTopStyle !== 'none' &&
    alpha(style.borderTopColor) > 0.05;
  return border || alpha(style.backgroundColor) > 0.05;
}

function describe(el: Element): string {
  const box = el.getBoundingClientRect();
  const label = el.getAttribute('data-testid') ?? el.tagName.toLowerCase();
  return `${label} (${Math.round(box.left)},${Math.round(box.top)} ${Math.round(box.width)}x${Math.round(box.height)})`;
}

function minGapFor(container: Element, side: 'acima' | 'abaixo'): number {
  if (container.classList.contains('mat-mdc-text-field-wrapper')) {
    return side === 'acima' ? MIN_GAP_ABOVE_FIELD : MIN_GAP;
  }
  if (container.matches('.mdc-button--outlined, .mdc-button--unelevated')) {
    return MIN_GAP_BUTTON;
  }
  return MIN_GAP;
}

/** Tudo o que está colado no que está na tela. Com um diálogo aberto, mede só o diálogo. */
export function findCrowding(doc: Document): Crowding[] {
  const root: Element = doc.querySelector('.mat-mdc-dialog-container') ?? doc.body;

  const texts: { box: Edge; node: Text; text: string }[] = [];
  const walker = doc.createTreeWalker(root, NodeFilter.SHOW_TEXT);
  while (walker.nextNode()) {
    const node = walker.currentNode as Text;
    const text = (node.textContent ?? '').trim();
    const parent = node.parentElement;
    if (
      text === '' ||
      parent === null ||
      parent.closest(IGNORED_TEXT) !== null ||
      !isVisible(doc, parent)
    ) {
      continue;
    }
    const range = doc.createRange();
    range.selectNodeContents(node);
    // Num diálogo com rolagem, o texto cortado pela borda da área visível não é vizinho de nada.
    const visible = parent.closest('.mat-mdc-dialog-content')?.getBoundingClientRect();
    for (const box of Array.from(range.getClientRects())) {
      const clipped =
        visible !== undefined && (box.top < visible.top - 0.5 || box.bottom > visible.bottom + 0.5);
      if (box.width >= 2 && box.height >= 2 && !clipped) {
        texts.push({ box, node, text: text.slice(0, 48) });
      }
    }
  }

  const found: Crowding[] = [];
  for (const container of Array.from(root.querySelectorAll(CONTAINERS))) {
    const box = container.getBoundingClientRect();
    if (
      box.width < 40 ||
      box.height < 24 ||
      !isVisible(doc, container) ||
      (!container.classList.contains('mat-mdc-text-field-wrapper') &&
        !hasBorderOrFill(doc, container))
    ) {
      continue;
    }
    // O texto do próprio campo (dica e erro ficam logo abaixo dele de propósito) não conta como vizinho.
    const ownField = container.closest('mat-form-field');
    let above: { gap: number; text: string } | null = null;
    let below: { gap: number; text: string } | null = null;
    for (const { box: t, node, text } of texts) {
      if (
        container.contains(node) ||
        ownField?.contains(node) === true ||
        Math.min(box.right, t.right) - Math.max(box.left, t.left) < 8
      ) {
        continue;
      }
      if (t.bottom <= box.top + 0.5) {
        const gap = box.top - t.bottom;
        above = gap <= 48 && (above === null || gap < above.gap) ? { gap, text } : above;
      } else if (t.top >= box.bottom - 0.5) {
        const gap = t.top - box.bottom;
        below = gap <= 48 && (below === null || gap < below.gap) ? { gap, text } : below;
      }
    }
    for (const [side, near] of [
      ['acima', above],
      ['abaixo', below],
    ] as const) {
      const min = minGapFor(container, side);
      if (near !== null && near.gap < min) {
        found.push({
          side,
          gap: Math.round(near.gap * 10) / 10,
          min,
          text: near.text,
          container: describe(container),
        });
      }
    }
  }
  return found;
}

function report(doc: Document): string[] {
  return findCrowding(doc).map(
    (c) =>
      `"${c.text}" a ${c.gap}px ${c.side === 'acima' ? 'acima de' : 'abaixo de'} ${c.container} (mínimo ${c.min}px)`,
  );
}

/** Falha, listando cada caso, se algum texto da tela estiver colado no container vizinho. Repete até a tela assentar. */
export function expectBreathingRoom(): void {
  cy.document().should((doc) => {
    const crowded = report(doc);
    expect(crowded, crowded.join('\n')).to.deep.equal([]);
  });
}

/** O mesmo para o diálogo aberto: espera o diálogo aparecer (e parar de animar) antes de medir. */
export function expectDialogBreathingRoom(): void {
  cy.document().should((doc) => {
    expect(doc.querySelector('.mat-mdc-dialog-container'), 'diálogo aberto').not.to.equal(null);
    const crowded = report(doc);
    expect(crowded, crowded.join('\n')).to.deep.equal([]);
  });
}
