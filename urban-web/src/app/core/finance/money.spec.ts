import { FormControl } from '@angular/forms';
import { formatMoney, formatMoneyInput, moneyField, parseMoney } from './money';

function plain(text: string): string {
  return text.replaceAll(' ', ' ');
}

describe('parseMoney', () => {
  it.each([
    ['1.234,56', 1234.56],
    ['1234,56', 1234.56],
    ['1234.56', 1234.56],
    ['R$ 1.234,56', 1234.56],
    ['r$1234,5', 1234.5],
    ['10', 10],
    ['0,01', 0.01],
    ['12.5', 12.5],
    ['1.234', 1234],
    ['  99,90  ', 99.9],
    ['9.999.999,99', 9999999.99],
  ])('reads "%s" as %f', (text, expected) => {
    expect(parseMoney(text)).toBe(expected);
  });

  it.each(['', '   ', 'R$', '0', '0,00', '-5', 'abc', '1,234', '1,2,3', '10.000.000', '12,5x'])(
    'refuses "%s"',
    (text) => {
      expect(parseMoney(text)).toBeNull();
    },
  );
});

describe('formatMoney', () => {
  it('writes reais with thousands, comma and two decimals', () => {
    expect(plain(formatMoney(1234.5))).toBe('R$ 1.234,50');
    expect(plain(formatMoney(0.05))).toBe('R$ 0,05');
    expect(plain(formatMoney(4000))).toBe('R$ 4.000,00');
  });

  it('writes a dash when there is no value', () => {
    expect(formatMoney(null)).toBe('—');
  });
});

describe('formatMoneyInput', () => {
  it('writes the number the way it is typed: comma and two decimals, no thousands', () => {
    expect(formatMoneyInput(1234.5)).toBe('1234,50');
    expect(formatMoneyInput(10)).toBe('10,00');
  });

  it('is empty when there is no value', () => {
    expect(formatMoneyInput(null)).toBe('');
  });

  it('can be read back by parseMoney', () => {
    expect(parseMoney(formatMoneyInput(8123.4))).toBe(8123.4);
  });
});

describe('moneyField', () => {
  it('accepts an amount the API accepts', () => {
    expect(moneyField(new FormControl('1.500,50', { nonNullable: true }))).toBeNull();
  });

  it.each(['', '0', 'abc', '1,234'])('flags "%s"', (text) => {
    expect(moneyField(new FormControl(text, { nonNullable: true }))).toEqual({ money: true });
  });
});
