/** @jest-environment node */
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import * as sass from 'sass';

const STYLES = join(process.cwd(), 'src', 'styles');

function compile(): string {
  return sass.compile(join(STYLES, '_autofill.scss')).css;
}

function ruleFor(css: string, selector: string): string {
  const rule = css.split('}').find((block) => block.includes(selector));
  return rule ?? '';
}

describe('preenchimento automático do navegador', () => {
  it('tira o fundo claro que o Chrome pinta no campo preenchido', () => {
    const rule = ruleFor(compile(), 'input:-webkit-autofill');

    expect(rule).toMatch(/transition:\s*background-color 600000s 0s/);
  });

  it('impõe o atraso com !important, para não perder para a transição que o Material põe no campo', () => {
    const rule = ruleFor(compile(), 'input:-webkit-autofill');

    expect(rule).toMatch(/transition:[^;]*600000s[^;]*!important/);
  });

  it('mantém o texto na cor do tema, sem depender da cor que o navegador escolhe', () => {
    const rule = ruleFor(compile(), 'input:-webkit-autofill');

    expect(rule).toContain('-webkit-text-fill-color: var(--mat-sys-on-surface)');
    expect(rule).toContain('caret-color: var(--mat-sys-on-surface)');
  });

  it('vale também com o campo em foco, com o mouse em cima ou sendo clicado', () => {
    const css = compile();

    for (const state of ['hover', 'focus', 'active']) {
      expect(css).toContain(`input:-webkit-autofill:${state}`);
    }
  });

  it('cobre o seletor padrão :autofill, que o Firefox usa', () => {
    expect(ruleFor(compile(), 'input:autofill')).toContain('background-color: transparent');
  });

  it('é carregado pelas folhas de estilo globais', () => {
    expect(readFileSync(join(STYLES, 'main.scss'), 'utf-8')).toContain("@use 'autofill';");
  });
});
