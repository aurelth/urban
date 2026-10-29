import { addressPatch } from './address-autofill';

const FOUND = {
  postalCode: '89010000',
  street: 'Rua XV de Novembro',
  neighborhood: 'Centro',
  city: 'Blumenau',
  state: 'SC',
};

describe('addressPatch', () => {
  it('leva rua, bairro, cidade e UF do CEP para o formulário', () => {
    expect(addressPatch(FOUND)).toEqual({
      street: 'Rua XV de Novembro',
      neighborhood: 'Centro',
      city: 'Blumenau',
      state: 'SC',
    });
  });

  it('nunca toca no CEP digitado, no número nem no complemento', () => {
    const keys = Object.keys(addressPatch(FOUND));

    expect(keys).not.toContain('postalCode');
    expect(keys).not.toContain('number');
    expect(keys).not.toContain('complement');
  });

  it('deixa de fora o que o CEP não informa, para não apagar o que a pessoa digitou', () => {
    expect(addressPatch({ ...FOUND, street: '', neighborhood: '   ' })).toEqual({
      city: 'Blumenau',
      state: 'SC',
    });
  });
});
