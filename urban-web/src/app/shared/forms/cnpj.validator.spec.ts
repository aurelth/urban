import { FormControl } from '@angular/forms';
import { cnpjNumber } from './cnpj.validator';

function check(value: string) {
  return cnpjNumber(new FormControl(value, { nonNullable: true }));
}

describe('cnpjNumber', () => {
  it.each([
    '11.222.333/0001-81',
    '11222333000181',
    '12.ABC.345/01DE-35',
    '12ABC34501DE35',
    '12.abc.345/01de-35',
    'AB.12C.D34/EF56-02',
  ])('aceita "%s"', (value) => {
    expect(check(value)).toBeNull();
  });

  it('deixa o campo vazio para o validador de obrigatoriedade', () => {
    expect(check('')).toBeNull();
  });

  it.each([
    '   ',
    '11.222.333/0001-80',
    '11.222.333/0001-91',
    '11.222.333/0001',
    '11.222.333/0001-811',
    '00.000.000/0000-00',
    '11.111.111/1111-11',
    '12.ABC.345/01DE-3X',
    '12.ABC.345/01DE-AB',
    '12.ABC.345/01DE-34',
    '12.ABC.345/01DF-35',
    '11.222.333\\0001-81',
    '11.222.333/0001-8*',
  ])('recusa "%s"', (value) => {
    expect(check(value)).toEqual({ cnpj: true });
  });
});
