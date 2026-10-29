import { FormControl } from '@angular/forms';
import { cpfNumber } from './cpf.validator';

function check(value: string) {
  return cpfNumber(new FormControl(value, { nonNullable: true }));
}

describe('cpfNumber', () => {
  it.each(['529.982.247-25', '52998224725', '123.456.789-09', '000.000.001-91'])(
    'aceita "%s"',
    (value) => {
      expect(check(value)).toBeNull();
    },
  );

  it('deixa o campo vazio para o validador de obrigatoriedade', () => {
    expect(check('')).toBeNull();
  });

  it.each([
    '   ',
    '529.982.247-24',
    '529.982.247-35',
    '529.982.247',
    '529.982.247-250',
    '111.111.111-11',
    '000.000.000-00',
    '529.982.247-2a',
    'abc.def.ghi-jk',
    '529/982/247-25',
  ])('recusa "%s"', (value) => {
    expect(check(value)).toEqual({ cpf: true });
  });

  it('confere os dois dígitos verificadores', () => {
    expect(check('529.982.247-05')).toEqual({ cpf: true });
    expect(check('529.982.247-20')).toEqual({ cpf: true });
  });
});
