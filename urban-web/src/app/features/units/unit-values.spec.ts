import {
  formatArea,
  formatAreaInput,
  formatPercent,
  formatPercentInput,
  fractionToPercent,
  parseArea,
  parsePercent,
  percentToFraction,
  unitLabel,
} from './unit-values';

describe('parsePercent', () => {
  it.each(['', '   '])('texto vazio ("%s") e valido e significa "sem fracao"', (text) => {
    expect(parsePercent(text)).toEqual({ valid: true, value: null });
  });

  it.each([
    ['1,25', 1.25],
    ['1.25', 1.25],
    ['1,25%', 1.25],
    [' 12 % ', 12],
    ['100', 100],
    ['100,000000', 100],
    ['0,000001', 0.000001],
    ['33,333333', 33.333333],
  ])('entende "%s" como %s', (text, value) => {
    expect(parsePercent(text)).toEqual({ valid: true, value });
  });

  it.each([
    '0',
    '0,0',
    '-1',
    '100,01',
    '101',
    'abc',
    '1,2,3',
    '1,',
    ',5',
    '1,1234567',
    '1e3',
    '1.250',
    '%',
  ])('recusa "%s"', (text) => {
    expect(parsePercent(text)).toEqual({ valid: false });
  });
});

describe('parseArea', () => {
  it('texto vazio e valido e significa "sem area"', () => {
    expect(parseArea('')).toEqual({ valid: true, value: null });
  });

  it.each([
    ['52,5', 52.5],
    ['52.5', 52.5],
    ['52', 52],
    ['0,01', 0.01],
    ['1.250', 1250],
    ['1.250,50', 1250.5],
    ['99999,99', 99999.99],
    ['99.999,99', 99999.99],
    [' 80 m² ', 80],
    ['80m2', 80],
  ])('entende "%s" como %s', (text, value) => {
    expect(parseArea(text)).toEqual({ valid: true, value });
  });

  it.each(['0', '0,00', '-5', '100000', '99999,991', '52,555', 'abc', '1,2,3', '1.2.3', '12,'])(
    'recusa "%s"',
    (text) => {
      expect(parseArea(text)).toEqual({ valid: false });
    },
  );
});

describe('conversao entre porcentagem e fracao', () => {
  it.each([
    [1.25, 0.0125],
    [100, 1],
    [50, 0.5],
    [33.333333, 0.33333333],
    [0.000001, 0.00000001],
    [12.345678, 0.12345678],
  ])('%s%% vira a fracao %s, sem sobra de ponto flutuante', (percent, fraction) => {
    expect(percentToFraction(percent)).toBe(fraction);
  });

  it.each([
    [0.0125, 1.25],
    [1, 100],
    [0.33333333, 33.333333],
    [0.00000001, 0.000001],
  ])('a fracao %s volta como %s%%', (fraction, percent) => {
    expect(fractionToPercent(fraction)).toBe(percent);
  });

  it('ida e volta nao altera o valor digitado', () => {
    for (const percent of [0.1, 0.2, 0.3, 7.7, 14.285714, 99.999999]) {
      expect(fractionToPercent(percentToFraction(percent))).toBe(percent);
    }
  });
});

describe('formatacao da fracao ideal', () => {
  it.each([
    [0.0125, '1,25'],
    [0.5, '50'],
    [1, '100'],
    [0.33333333, '33,333333'],
    [null, ''],
  ])('no campo, %s aparece como "%s"', (fraction, text) => {
    expect(formatPercentInput(fraction)).toBe(text);
  });

  it.each([
    [0.0125, '1,25%'],
    [1, '100%'],
    [0.33333333, '33,333333%'],
    [null, '—'],
  ])('na lista, %s aparece como "%s"', (fraction, text) => {
    expect(formatPercent(fraction)).toBe(text);
  });

  it('o texto do campo volta ao mesmo valor ao ser lido de novo', () => {
    const text = formatPercentInput(0.07142857);

    expect(parsePercent(text)).toEqual({ valid: true, value: fractionToPercent(0.07142857) });
  });
});

describe('formatacao da area', () => {
  it.each([
    [52.5, '52,5'],
    [80, '80'],
    [1250.5, '1250,5'],
    [0.01, '0,01'],
    [null, ''],
  ])('no campo, %s aparece como "%s"', (area, text) => {
    expect(formatAreaInput(area)).toBe(text);
  });

  it.each([
    [52.5, '52,5 m²'],
    [80, '80 m²'],
    [1250.5, '1.250,5 m²'],
    [null, '—'],
  ])('na lista, %s aparece como "%s"', (area, text) => {
    expect(formatArea(area)).toBe(text);
  });

  it('o texto do campo volta ao mesmo valor ao ser lido de novo', () => {
    expect(parseArea(formatAreaInput(1250.5))).toEqual({ valid: true, value: 1250.5 });
  });
});

describe('unitLabel', () => {
  it('junta o bloco e o numero', () => {
    expect(unitLabel({ block: 'A', number: '101' })).toBe('Bloco A · 101');
  });

  it.each([null, ''])('sem bloco (%p), mostra so o numero', (block) => {
    expect(unitLabel({ block, number: '12' })).toBe('12');
  });
});
