import { FormControl } from '@angular/forms';
import { licensePlate } from './license-plate.validator';

function check(value: string) {
  return licensePlate(new FormControl(value, { nonNullable: true }));
}

describe('licensePlate', () => {
  it.each(['ABC1234', 'ABC1D23', 'abc1d23', 'ABC-1234', 'abc 1d23'])('aceita "%s"', (value) => {
    expect(check(value)).toBeNull();
  });

  it('deixa o campo vazio para o validador de obrigatoriedade', () => {
    expect(check('')).toBeNull();
  });

  it.each(['ABC123', 'ABC12345', 'AB1D234', 'ABCD123', 'ABC1DD3', '1234ABC', 'ÁBC1234', 'ABC*234'])(
    'recusa "%s"',
    (value) => {
      expect(check(value)).toEqual({ licensePlate: true });
    },
  );
});
