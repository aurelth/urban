import { announcement } from '../../testing/announcements.fixtures';
import { unit } from '../../testing/units.fixtures';
import {
  CATEGORY_OPTIONS,
  categoryClasses,
  categoryLabel,
  formatMoment,
  readersClasses,
  readersLabel,
  scopeClasses,
  scopeLabel,
} from './announcement-values';

describe('categoryLabel', () => {
  it.each([
    ['general', 'Geral'],
    ['maintenance', 'Manutenção'],
    ['important', 'Importante'],
  ] as const)('%s vira "%s"', (category, label) => {
    expect(categoryLabel(category)).toBe(label);
  });

  it('as opções de escolha são as mesmas três, na mesma ordem', () => {
    expect(CATEGORY_OPTIONS.map((option) => option.value)).toEqual([
      'general',
      'maintenance',
      'important',
    ]);
  });
});

describe('scopeLabel', () => {
  const target = unit({ block: 'B', number: '202' });

  it('diz "Condomínio inteiro" quando vai para todos', () => {
    expect(scopeLabel(announcement(), [target])).toBe('Condomínio inteiro');
  });

  it('diz o bloco quando vai para um bloco', () => {
    expect(scopeLabel(announcement({ scope: 'block', block: 'B' }), [target])).toBe('Bloco B');
  });

  it('diz o número e o bloco da unidade quando vai para uma unidade', () => {
    expect(scopeLabel(announcement({ scope: 'unit', unitId: target.id }), [target])).toBe(
      'Unidade 202 do bloco B',
    );
  });

  it('diz só o número quando a unidade não tem bloco', () => {
    const house = unit({ block: null, number: '12' });

    expect(scopeLabel(announcement({ scope: 'unit', unitId: house.id }), [house])).toBe(
      'Unidade 12',
    );
  });

  it('não inventa uma unidade que saiu da lista das ativas', () => {
    expect(scopeLabel(announcement({ scope: 'unit', unitId: 'outra' }), [target])).toBe(
      'Uma unidade que não está mais ativa',
    );
  });
});

describe('readersLabel', () => {
  it.each([
    [3, 10, '3 de 10 leram'],
    [0, 10, '0 de 10 leram'],
    [10, 10, '10 de 10 leram'],
    [1, 1, '1 de 1 leu'],
    [0, 1, '0 de 1 leu'],
  ])('%s de %s vira "%s"', (readers, recipients, label) => {
    expect(readersLabel({ readers, recipients })).toBe(label);
  });

  it('sem ninguém no alcance, diz que não há quem leia', () => {
    expect(readersLabel({ readers: 0, recipients: 0 })).toBe('Ninguém mora aqui ainda para ler');
  });
});

describe('formatMoment', () => {
  it('mostra dia, mês, ano e hora de Brasília', () => {
    expect(formatMoment('2026-11-22T13:50:00Z')).toBe('22/11/2026 às 10:50');
  });

  it('vira o dia pela hora de Brasília, não pela do computador', () => {
    expect(formatMoment('2026-11-23T02:30:00Z')).toBe('22/11/2026 às 23:30');
  });

  it('devolve o texto como veio quando não é uma data', () => {
    expect(formatMoment('ontem')).toBe('ontem');
  });
});

describe('cores das etiquetas', () => {
  it.each([
    ['general', 'bg-tag-blue-container text-tag-blue'],
    ['maintenance', 'bg-tag-amber-container text-tag-amber'],
    ['important', 'bg-tag-red-container text-tag-red'],
  ] as const)('o grau %s usa "%s"', (category, classes) => {
    expect(categoryClasses(category)).toBe(classes);
  });

  it.each([
    ['condominium', 'bg-tag-teal-container text-tag-teal'],
    ['block', 'bg-tag-purple-container text-tag-purple'],
    ['unit', 'bg-tag-pink-container text-tag-pink'],
  ] as const)('o público %s usa "%s"', (scope, classes) => {
    expect(scopeClasses(scope)).toBe(classes);
  });

  it('cada grau tem uma cor só dele, e cada público também', () => {
    const categories = (['general', 'maintenance', 'important'] as const).map(categoryClasses);
    const scopes = (['condominium', 'block', 'unit'] as const).map(scopeClasses);

    expect(new Set(categories).size).toBe(3);
    expect(new Set(scopes).size).toBe(3);
  });

  it('o grau e o público nunca dividem uma cor', () => {
    const categories = (['general', 'maintenance', 'important'] as const).map(categoryClasses);
    const scopes = (['condominium', 'block', 'unit'] as const).map(scopeClasses);

    expect(categories.filter((classes) => scopes.includes(classes))).toEqual([]);
  });

  it('quantos leram ganha o verde quando há quem leia, e fica neutro quando não há', () => {
    expect(readersClasses({ recipients: 10 })).toBe('bg-tag-green-container text-tag-green');
    expect(readersClasses({ recipients: 0 })).toBe('text-on-surface-variant');
  });
});
