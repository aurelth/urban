/** @jest-environment node */
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

function style(file: string): string {
  return readFileSync(join(process.cwd(), 'src', 'styles', file), 'utf8');
}

const SCROLLBAR = style('_scrollbar.scss');

function rule(selector: string): string {
  const start = SCROLLBAR.indexOf(`${selector} {`);
  return start < 0 ? '' : (SCROLLBAR.slice(start).split('}')[0] ?? '');
}

describe('barra de rolagem', () => {
  it('entra no tema global', () => {
    expect(style('main.scss')).toMatch(/@use 'scrollbar';/);
  });

  it('é fina: no máximo 10 px de espaço, com o traço visível de 6 px, arredondado, em Chrome, Edge e Safari', () => {
    const bar = rule('::-webkit-scrollbar');
    const thumb = rule('::-webkit-scrollbar-thumb');

    expect(bar).toMatch(/width:\s*10px/);
    expect(bar).toMatch(/height:\s*10px/);
    expect(thumb).toMatch(/border:\s*2px solid transparent/);
    expect(thumb).toMatch(/background-clip:\s*padding-box/);
    expect(thumb).toMatch(/border-radius:\s*999px/);
  });

  it('deixa a trilha transparente, para só o traço aparecer', () => {
    expect(rule('::-webkit-scrollbar-track')).toMatch(/background:\s*transparent/);
    expect(rule('::-webkit-scrollbar-corner')).toMatch(/background:\s*transparent/);
  });

  it('usa a propriedade padrão, fina, só onde não há a versão -webkit (Firefox)', () => {
    const fallback = SCROLLBAR.split('@supports not selector(::-webkit-scrollbar)')[1] ?? '';

    expect(fallback).toMatch(/scrollbar-width:\s*thin/);
    expect(fallback).toMatch(/scrollbar-color:\s*var\(--urban-scrollbar-thumb\) transparent/);
  });

  it('escurece o traço ao passar o mouse', () => {
    expect(rule('::-webkit-scrollbar-thumb:hover')).toMatch(
      /background-color:\s*var\(--urban-scrollbar-thumb-hover\)/,
    );
  });

  it('tira as cores do traço dos tokens, que trocam sozinhos no modo escuro', () => {
    const tokens = style('_tokens.scss');

    expect(tokens).toMatch(/--urban-scrollbar-thumb:\s*light-dark\(/);
    expect(tokens).toMatch(/--urban-scrollbar-thumb-hover:\s*light-dark\(/);
    expect(rule('::-webkit-scrollbar-thumb')).toMatch(
      /background-color:\s*var\(--urban-scrollbar-thumb\)/,
    );
  });
});
