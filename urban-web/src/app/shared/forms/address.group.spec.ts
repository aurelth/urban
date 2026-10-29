import { TestBed } from '@angular/core/testing';
import { NonNullableFormBuilder } from '@angular/forms';
import { addressGroup } from './address.group';

const VALID = {
  postalCode: '89010-000',
  street: 'Rua das Flores',
  number: '120',
  complement: '',
  neighborhood: 'Centro',
  city: 'Blumenau',
  state: 'SC',
};

function build(value: Partial<typeof VALID> = {}) {
  const group = addressGroup(TestBed.inject(NonNullableFormBuilder));
  group.setValue({ ...VALID, ...value });
  return group;
}

describe('addressGroup', () => {
  it('começa vazio, com os sete campos do endereço', () => {
    const group = addressGroup(TestBed.inject(NonNullableFormBuilder));

    expect(group.getRawValue()).toEqual({
      postalCode: '',
      street: '',
      number: '',
      complement: '',
      neighborhood: '',
      city: '',
      state: '',
    });
    expect(group.invalid).toBe(true);
  });

  it('aceita um endereço completo, com ou sem complemento', () => {
    expect(build().valid).toBe(true);
    expect(build({ complement: 'Bloco B, apto 42' }).valid).toBe(true);
  });

  it.each([
    ['postalCode', '', 'required'],
    ['postalCode', '1234', 'postalCode'],
    ['postalCode', '00000-000', 'postalCode'],
    ['street', '', 'required'],
    ['street', '   ', 'required'],
    ['street', 'a'.repeat(121), 'maxlength'],
    ['number', '', 'required'],
    ['number', '12345678901', 'maxlength'],
    ['complement', 'a'.repeat(61), 'maxlength'],
    ['neighborhood', '', 'required'],
    ['neighborhood', 'a'.repeat(81), 'maxlength'],
    ['city', '', 'required'],
    ['city', 'a'.repeat(81), 'maxlength'],
    ['state', '', 'required'],
  ] as const)('recusa %s = "%s" com o erro %s', (field, value, error) => {
    const group = build({ [field]: value });

    expect(group.controls[field].hasError(error)).toBe(true);
    expect(group.invalid).toBe(true);
  });

  it('aceita os tamanhos máximos', () => {
    const group = build({
      street: 'a'.repeat(120),
      number: '1'.repeat(10),
      complement: 'a'.repeat(60),
      neighborhood: 'a'.repeat(80),
      city: 'a'.repeat(80),
    });

    expect(group.valid).toBe(true);
  });
});
