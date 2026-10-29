import { FormControl } from '@angular/forms';
import { postalCodeFormat } from './postal-code.validator';

function check(value: string) {
  return postalCodeFormat(new FormControl(value, { nonNullable: true }));
}

describe('postalCodeFormat', () => {
  it.each(['89010-000', '89010000', '01001-000', '89.010-000'])('aceita "%s"', (value) => {
    expect(check(value)).toBeNull();
  });

  it('deixa o campo vazio para o validador de obrigatoriedade', () => {
    expect(check('')).toBeNull();
  });

  it.each(['   ', '8901-000', '89010-00', '89010-0000', '00000-000', '8901a-000', '89010 000x'])(
    'recusa "%s"',
    (value) => {
      expect(check(value)).toEqual({ postalCode: true });
    },
  );
});
