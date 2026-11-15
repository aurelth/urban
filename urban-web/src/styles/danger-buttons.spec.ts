/** @jest-environment node */
import { readFileSync, readdirSync } from 'node:fs';
import { join } from 'node:path';

const THEME = readFileSync(join(process.cwd(), 'src', 'styles', '_material-theme.scss'), 'utf8');

function templates(dir: string): string[] {
  return readdirSync(dir, { withFileTypes: true }).flatMap((entry) => {
    const path = join(dir, entry.name);
    if (entry.isDirectory()) {
      return templates(path);
    }
    return entry.name.endsWith('.html') ? [path] : [];
  });
}

describe('botões de exclusão', () => {
  it('o tema define o estilo urban-danger para os três tipos de botão, pelo papel error do Material', () => {
    const block = THEME.split('.urban-danger {')[1]?.split('\n}')[0] ?? '';

    expect(block).toContain('mat.icon-button-overrides');
    expect(block).toContain('mat.button-overrides');
    expect(block).toMatch(/icon-color:\s*var\(--mat-sys-error\)/);
    expect(block).toMatch(/filled-container-color:\s*var\(--mat-sys-error\)/);
    expect(block).toMatch(/filled-label-text-color:\s*var\(--mat-sys-on-error\)/);
  });

  it('todo botão que exclui, apaga ou remove (ícone de lixeira ou texto) leva urban-danger', () => {
    const missing: string[] = [];
    for (const file of templates(join(process.cwd(), 'src', 'app'))) {
      for (const [button] of readFileSync(file, 'utf8').matchAll(/<button\b[\s\S]*?<\/button>/g)) {
        const opening = button.slice(0, button.indexOf('>') + 1);
        const inner = button.slice(opening.length);
        const destroys =
          /svgIcon="delete"/.test(button) || /\b(Excluir|Apagar|Remover)\b/.test(inner);
        if (destroys && !/\burban-danger\b/.test(opening)) {
          missing.push(`${file.replace(process.cwd(), '')}: ${opening.replace(/\s+/g, ' ')}`);
        }
      }
    }

    expect(missing).toEqual([]);
  });
});
