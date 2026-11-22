import { jest } from '@jest/globals';
import { MonthNavigation } from './month-navigation';

const NOW = (): Date => new Date('2026-11-15T15:00:00Z');

function setup(requested: string | null = null): {
  navigation: MonthNavigation;
  load: jest.Mock<(month: string) => Promise<void>>;
} {
  const load = jest.fn<(month: string) => Promise<void>>().mockResolvedValue(undefined);
  return { navigation: new MonthNavigation(requested, NOW, load), load };
}

describe('MonthNavigation', () => {
  it('começa no mês de hoje e só lê o mês quando a tela manda começar', async () => {
    const { navigation, load } = setup();

    expect(navigation.month()).toBe('2026-11');
    expect(load).not.toHaveBeenCalled();

    await navigation.start();

    expect(load).toHaveBeenCalledWith('2026-11');
  });

  it('começa no mês do endereço quando ele vale e no de hoje quando não vale', () => {
    expect(setup('2026-09').navigation.month()).toBe('2026-09');
    expect(setup('2099-01').navigation.month()).toBe('2026-11');
    expect(setup('lixo').navigation.month()).toBe('2026-11');
  });

  it('anda de mês em mês e lê cada mês novo', async () => {
    const { navigation, load } = setup();

    await navigation.go(-1);
    expect(navigation.month()).toBe('2026-10');
    await navigation.go(2);
    expect(navigation.month()).toBe('2026-12');

    expect(load.mock.calls.map(([month]) => month)).toEqual(['2026-10', '2026-12']);
  });

  it('lê de novo o mês que está na tela', async () => {
    const { navigation, load } = setup('2026-09');

    await navigation.reload();

    expect(navigation.month()).toBe('2026-09');
    expect(load).toHaveBeenCalledWith('2026-09');
  });

  it('não deixa passar de 5 anos para a frente nem para trás', () => {
    expect(setup('2031-11').navigation.canGo(1)).toBe(false);
    expect(setup('2031-11').navigation.canGo(-1)).toBe(true);
    expect(setup('2021-11').navigation.canGo(-1)).toBe(false);
    expect(setup('2021-11').navigation.canGo(1)).toBe(true);
  });
});
