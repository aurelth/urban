/** @jest-environment node */
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const THEME = readFileSync(join(process.cwd(), 'src', 'styles', '_material-theme.scss'), 'utf8');

function lightColors(): Record<string, string> {
  const block = THEME.split('html:not(.app-dark) {')[1]?.split('\n}')[0] ?? '';
  const colors: Record<string, string> = {};
  for (const [, role, hex] of block.matchAll(/^\s+([a-z-]+): (#[0-9a-f]{6}),?$/gm)) {
    colors[role] = hex;
  }
  return colors;
}

function luminance(hex: string): number {
  const [r, g, b] = [1, 3, 5].map((i) => {
    const channel = parseInt(hex.slice(i, i + 2), 16) / 255;
    return channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contrast(a: string, b: string): number {
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (light + 0.05) / (dark + 0.05);
}

describe('tema claro: contraste entre fundo, cards e bordas', () => {
  const colors = lightColors();

  it('lê as cores do bloco do tema claro', () => {
    expect(colors['surface']).toMatch(/^#[0-9a-f]{6}$/);
    expect(colors['surface-container']).toBe('#ffffff');
    expect(colors['outline-variant']).toMatch(/^#[0-9a-f]{6}$/);
  });

  it('usa o fundo e a borda da paleta "Índigo suave" do CLAUDE.md', () => {
    expect(colors['surface']).toBe('#fcfcfd');
    expect(colors['outline-variant']).toBe('#e6e6ef');
  });

  it('deixa o fundo da página quase branco, como o fundo claro de referência, e o card branco', () => {
    expect(luminance(colors['surface'])).toBeGreaterThanOrEqual(0.95);
    expect(colors['surface-container']).toBe('#ffffff');
    expect(luminance(colors['surface-container'])).toBeGreaterThanOrEqual(
      luminance(colors['surface']),
    );
  });

  it('mantém o texto secundário legível sobre o fundo da página (AA, 4,5:1)', () => {
    expect(contrast(colors['on-surface-variant'], colors['surface'])).toBeGreaterThanOrEqual(4.5);
  });

  it('mantém o texto principal bem legível sobre o fundo e sobre o card', () => {
    expect(contrast(colors['on-surface'], colors['surface'])).toBeGreaterThanOrEqual(12);
    expect(contrast(colors['on-surface'], colors['surface-container'])).toBeGreaterThanOrEqual(12);
  });

  it('desenha a borda dos cards visível tanto sobre o card quanto sobre o fundo', () => {
    expect(contrast(colors['outline-variant'], colors['surface-container'])).toBeGreaterThanOrEqual(
      1.2,
    );
    expect(contrast(colors['outline-variant'], colors['surface'])).toBeGreaterThanOrEqual(1.1);
  });

  it('deixa o contorno dos campos mais forte que a borda dos cards', () => {
    expect(contrast(colors['outline'], colors['surface-container'])).toBeGreaterThan(
      contrast(colors['outline-variant'], colors['surface-container']),
    );
  });

  it('mantém a superfície mais alta mais escura que o fundo, como era antes', () => {
    expect(luminance(colors['surface-container-highest'])).toBeLessThan(
      luminance(colors['surface']),
    );
  });
});

function templates(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) {
      return templates(path);
    }
    return path.endsWith('.html') ? [path] : [];
  });
}

describe('caixas com borda nas telas', () => {
  it('têm o fundo de card (branco no claro, grafite no escuro), senão somem no fundo da página', () => {
    const bare: string[] = [];
    for (const file of templates(join(process.cwd(), 'src', 'app'))) {
      for (const [, classes] of readFileSync(file, 'utf8').matchAll(/class="([^"]*)"/g)) {
        const isBox =
          /\brounded-(xl|lg)\b/.test(classes) && /\bborder-outline-variant\b/.test(classes);
        const isField = classes.includes('bg-transparent');
        if (isBox && !isField && !classes.includes('bg-surface-container')) {
          bare.push(`${file.replace(process.cwd(), '')}: ${classes}`);
        }
      }
    }

    expect(bare).toEqual([]);
  });
});
