import { formatDecimalInput, parseDecimal } from './decimal';

describe('parseDecimal', () => {
  it.each([
    ['0', 2, 0],
    ['0,00', 2, 0],
    ['10', 2, 10],
    ['8,5', 4, 8.5],
    ['8.5', 4, 8.5],
    ['8,5000', 4, 8.5],
    ['1.250', 3, 1250],
    ['1.250,5', 3, 1250.5],
    ['0,001', 3, 0.001],
    ['  100  ', 2, 100],
    ['99,99', 2, 99.99],
  ])('reads "%s" with up to %i decimals as %f', (text, decimals, expected) => {
    expect(parseDecimal(text, decimals)).toBe(expected);
  });

  it.each([
    ['', 2],
    ['   ', 2],
    ['abc', 2],
    ['-1', 2],
    ['1,234', 2],
    ['0,00001', 4],
    ['1,2,3', 2],
    ['12,5x', 2],
    ['R$ 10', 2],
  ])('refuses "%s" with up to %i decimals', (text, decimals) => {
    expect(parseDecimal(text, decimals)).toBeNull();
  });
});

describe('formatDecimalInput', () => {
  it('writes the number with a comma and no useless decimals', () => {
    expect(formatDecimalInput(5)).toBe('5');
    expect(formatDecimalInput(2.5)).toBe('2,5');
    expect(formatDecimalInput(0.0001)).toBe('0,0001');
    expect(formatDecimalInput(100000)).toBe('100000');
  });

  it('pads the decimals up to the minimum', () => {
    expect(formatDecimalInput(8.5, 2)).toBe('8,50');
    expect(formatDecimalInput(10, 2)).toBe('10,00');
    expect(formatDecimalInput(0, 2)).toBe('0,00');
    expect(formatDecimalInput(2.4, 1)).toBe('2,4');
    expect(formatDecimalInput(0.1234, 2)).toBe('0,1234');
  });
});
