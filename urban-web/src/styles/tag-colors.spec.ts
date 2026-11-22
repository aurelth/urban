/** @jest-environment node */
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const TOKENS = readFileSync(join(process.cwd(), 'src', 'styles', '_tokens.scss'), 'utf8');
const TAILWIND = readFileSync(join(process.cwd(), 'src', 'styles', 'tailwind.css'), 'utf8');
const THEME = readFileSync(join(process.cwd(), 'src', 'styles', '_material-theme.scss'), 'utf8');

const COLORS = ['blue', 'amber', 'red', 'teal', 'purple', 'pink', 'green'];
const TINT = 0.14;

function pair(color: string): { light: string; dark: string } {
  const match = TOKENS.match(
    new RegExp(`--urban-tag-${color}: light-dark\\((#[0-9a-f]{6}), (#[0-9a-f]{6})\\);`),
  );
  return { light: match?.[1] ?? '', dark: match?.[2] ?? '' };
}

function surface(theme: 'light' | 'dark'): string {
  const block = theme === 'light' ? 'html:not(.app-dark) {' : 'html.app-dark {';
  const part = THEME.split(block)[1] ?? '';
  return part.match(/surface-container: (#[0-9a-f]{6})/)?.[1] ?? '';
}

function channels(hex: string): number[] {
  return [1, 3, 5].map((i) => parseInt(hex.slice(i, i + 2), 16));
}

function luminance(rgb: number[]): number {
  const [r, g, b] = rgb.map((value) => {
    const channel = value / 255;
    return channel <= 0.03928 ? channel / 12.92 : ((channel + 0.055) / 1.055) ** 2.4;
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contrast(a: number[], b: number[]): number {
  const [light, dark] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (light + 0.05) / (dark + 0.05);
}

/** O fundo da etiqueta é a própria cor a 14% por cima do card. */
function tinted(color: string, card: string): number[] {
  const base = channels(card);
  return channels(color).map((value, index) => value * TINT + base[index] * (1 - TINT));
}

describe('cores das etiquetas dos comunicados', () => {
  it.each(COLORS)('%s está nos tokens, com versão para o tema claro e para o escuro', (color) => {
    const { light, dark } = pair(color);

    expect(light).toMatch(/^#[0-9a-f]{6}$/);
    expect(dark).toMatch(/^#[0-9a-f]{6}$/);
    expect(light).not.toBe(dark);
  });

  it.each(COLORS)('%s tem o fundo translúcido da própria cor', (color) => {
    expect(TOKENS).toContain(
      `--urban-tag-${color}-container: color-mix(in srgb, var(--urban-tag-${color}) 14%, transparent);`,
    );
  });

  it.each(COLORS)('%s vira utilitário do Tailwind, para o texto e para o fundo', (color) => {
    expect(TAILWIND).toContain(`--color-tag-${color}: var(--urban-tag-${color});`);
    expect(TAILWIND).toContain(
      `--color-tag-${color}-container: var(--urban-tag-${color}-container);`,
    );
  });

  it.each(COLORS)('%s é legível sobre o próprio fundo nos dois temas (AA, 4,5:1)', (color) => {
    const { light, dark } = pair(color);

    expect(contrast(channels(light), tinted(light, surface('light')))).toBeGreaterThanOrEqual(4.5);
    expect(contrast(channels(dark), tinted(dark, surface('dark')))).toBeGreaterThanOrEqual(4.5);
  });

  it('as sete cores são diferentes entre si no tema claro', () => {
    expect(new Set(COLORS.map((color) => pair(color).light)).size).toBe(COLORS.length);
  });
});
