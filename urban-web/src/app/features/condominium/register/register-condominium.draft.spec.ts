import { RegisterCondominiumFormValue } from './register-condominium.payload';
import { toDraftData } from './register-condominium.draft';

const ADDRESS = {
  postalCode: '89010-000',
  street: 'Rua das Flores',
  number: '120',
  complement: 'Apto 42',
  neighborhood: 'Centro',
  city: 'Blumenau',
  state: 'SC',
};

const FORM: RegisterCondominiumFormValue = {
  manager: {
    fullName: 'Marina Souza',
    displayName: 'Marina',
    cpf: '529.982.247-25',
    birthDate: '15/06/1980',
    phone: '(47) 99123-4567',
    address: ADDRESS,
  },
  condominium: {
    name: 'Residencial das Flores',
    cnpj: '11.222.333/0001-81',
    address: { ...ADDRESS, postalCode: '89012-100' },
  },
};

describe('toDraftData', () => {
  it('guarda o que a pessoa ja preencheu, do jeito que aparece nos campos', () => {
    expect(toDraftData(FORM, true)).toEqual({
      manager: {
        fullName: 'Marina Souza',
        displayName: 'Marina',
        phone: '(47) 99123-4567',
        address: ADDRESS,
      },
      condominium: {
        name: 'Residencial das Flores',
        cnpj: '11.222.333/0001-81',
        address: { ...ADDRESS, postalCode: '89012-100' },
      },
      sameAddress: true,
    });
  });

  it.each([
    ['o CPF', 'cpf', '529.982.247-25', '52998224725'],
    ['a data de nascimento', 'birthDate', '15/06/1980', '15061980'],
    ['a senha', 'password', 'Senha-segura-123', 'Senha-segura-123'],
  ])('nunca guarda %s', (_name, field, shown, digits) => {
    const withField = {
      ...FORM,
      manager: { ...FORM.manager, [field]: shown },
    } as RegisterCondominiumFormValue;

    const stored = JSON.stringify(toDraftData(withField, false));

    expect(stored).not.toContain(`"${field}"`);
    expect(stored).not.toContain(shown);
    expect(stored).not.toContain(digits);
  });

  it('nao leva campos que a tela venha a ganhar, so a lista combinada', () => {
    const extra = {
      ...FORM,
      manager: { ...FORM.manager, address: { ...ADDRESS, geo: '-26.9,-49.0' } },
    } as RegisterCondominiumFormValue;

    expect(JSON.stringify(toDraftData(extra, false))).not.toContain('geo');
  });
});
