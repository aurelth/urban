import { parseChange } from './announcement-change';

const ID = '0199cccc-0000-7000-8000-000000000001';

describe('parseChange', () => {
  it.each(['published', 'updated', 'removed'] as const)('lê o aviso %s', (kind) => {
    expect(parseChange({ kind, id: ID })).toEqual({ kind, id: ID });
  });

  it('ignora o que sobra no objeto', () => {
    expect(parseChange({ kind: 'published', id: ID, title: 'Corte de água' })).toEqual({
      kind: 'published',
      id: ID,
    });
  });

  it.each([
    ['nulo', null],
    ['texto', 'published'],
    ['número', 7],
    ['lista', [{ kind: 'published', id: ID }]],
    ['sem tipo', { id: ID }],
    ['sem id', { kind: 'published' }],
    ['tipo desconhecido', { kind: 'deleted', id: ID }],
    ['tipo que não é texto', { kind: 1, id: ID }],
    ['id vazio', { kind: 'published', id: '' }],
    ['id que não é texto', { kind: 'published', id: 5 }],
  ])('descarta %s', (_name, message) => {
    expect(parseChange(message)).toBeNull();
  });
});
