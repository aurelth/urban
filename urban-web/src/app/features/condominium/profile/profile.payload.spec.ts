import { MyCondominium } from '../../../core/condominium/condominium.models';
import {
  ProfileFormValue,
  freeFieldsChanged,
  identityChanged,
  toFreePayload,
  toIdentityPayload,
  toProfileFormValue,
} from './profile.payload';

const ADDRESS = {
  postalCode: '89010000',
  street: 'Rua das Flores',
  number: '120',
  complement: null,
  neighborhood: 'Centro',
  city: 'Blumenau',
  state: 'SC',
};

const MINE: MyCondominium = {
  condominiumId: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c4d5e',
  version: '7.9',
  displayName: 'Marina',
  manager: {
    fullName: 'Marina Souza',
    maskedCpf: '***.982.247-**',
    birthDate: '1980-06-15',
    phone: '47991234567',
    address: { ...ADDRESS, complement: 'Apto 42' },
  },
  condominium: {
    name: 'Residencial das Flores',
    cnpj: '11.222.333/0001-81',
    address: { ...ADDRESS, postalCode: '89012100', street: 'Rua XV de Novembro', number: '500' },
  },
};

function form(): ProfileFormValue {
  return toProfileFormValue(MINE);
}

function withManager(changes: Partial<ProfileFormValue['manager']>): ProfileFormValue {
  const value = form();
  return { ...value, manager: { ...value.manager, ...changes } };
}

function withCondominium(changes: Partial<ProfileFormValue['condominium']>): ProfileFormValue {
  const value = form();
  return { ...value, condominium: { ...value.condominium, ...changes } };
}

describe('toProfileFormValue', () => {
  it('mostra os dados como aparecem nos campos, com as mascaras', () => {
    expect(form()).toEqual({
      manager: {
        fullName: 'Marina Souza',
        displayName: 'Marina',
        cpf: '',
        birthDate: '15/06/1980',
        phone: '(47) 99123-4567',
        address: {
          postalCode: '89010-000',
          street: 'Rua das Flores',
          number: '120',
          complement: 'Apto 42',
          neighborhood: 'Centro',
          city: 'Blumenau',
          state: 'SC',
        },
      },
      condominium: {
        name: 'Residencial das Flores',
        address: {
          postalCode: '89012-100',
          street: 'Rua XV de Novembro',
          number: '500',
          complement: '',
          neighborhood: 'Centro',
          city: 'Blumenau',
          state: 'SC',
        },
      },
    });
  });

  it('nunca poe o CPF no campo: so a versao mascarada existe, e fora do formulario', () => {
    expect(JSON.stringify(form())).not.toContain('982');
    expect(form().manager.cpf).toBe('');
  });

  it('trata o nome de exibicao ausente como texto vazio', () => {
    expect(toProfileFormValue({ ...MINE, displayName: null }).manager.displayName).toBe('');
  });
});

describe('toFreePayload', () => {
  it('manda a versao lida e os campos livres, sem mascaras e com complemento nulo quando vazio', () => {
    expect(toFreePayload(form(), '7.9')).toEqual({
      version: '7.9',
      displayName: 'Marina',
      phone: '47991234567',
      managerAddress: { ...ADDRESS, complement: 'Apto 42' },
      condominiumName: 'Residencial das Flores',
      condominiumAddress: {
        ...ADDRESS,
        postalCode: '89012100',
        street: 'Rua XV de Novembro',
        number: '500',
        complement: null,
      },
    });
  });

  it('tira os espacos sobrando dos textos', () => {
    const value = withManager({ displayName: '  Marina   Souza ', phone: '(47)  99123-4567' });

    const payload = toFreePayload(value, '7.9');

    expect(payload.displayName).toBe('Marina Souza');
    expect(payload.phone).toBe('47991234567');
  });

  it('nao leva CPF, nome completo, data de nascimento nem CNPJ', () => {
    const keys = Object.keys(toFreePayload(form(), '7.9'));

    expect(keys).not.toEqual(expect.arrayContaining(['cpf']));
    expect(keys).not.toEqual(expect.arrayContaining(['fullName']));
    expect(keys).not.toEqual(expect.arrayContaining(['birthDate']));
    expect(keys).not.toEqual(expect.arrayContaining(['cnpj']));
    expect(JSON.stringify(toFreePayload(form(), '7.9'))).not.toContain('11222333000181');
  });
});

describe('toIdentityPayload', () => {
  it('manda a versao, a senha, o nome e a data em aaaa-mm-dd, sem CPF quando ele nao foi trocado', () => {
    const payload = toIdentityPayload(form(), '7.9', 'Senha-segura-123');

    expect(payload).toEqual({
      version: '7.9',
      password: 'Senha-segura-123',
      fullName: 'Marina Souza',
      birthDate: '1980-06-15',
    });
    expect(Object.keys(payload)).not.toContain('cpf');
  });

  it('manda o CPF novo so em digitos quando a pessoa o trocou', () => {
    const payload = toIdentityPayload(
      withManager({ cpf: '111.444.777-35' }),
      '7.9',
      'Senha-segura-123',
    );

    expect(payload.cpf).toBe('11144477735');
  });

  it('manda a data como texto vazio quando ela esta incompleta, para a API recusar', () => {
    const payload = toIdentityPayload(
      withManager({ birthDate: '15/06/19' }),
      '7.9',
      'Senha-segura-123',
    );

    expect(payload.birthDate).toBe('');
  });

  it('tira os espacos sobrando do nome', () => {
    const payload = toIdentityPayload(
      withManager({ fullName: ' Marina   Souza  Lima ' }),
      '7.9',
      'x',
    );

    expect(payload.fullName).toBe('Marina Souza Lima');
  });
});

describe('freeFieldsChanged', () => {
  it('e falso quando nada mudou', () => {
    expect(freeFieldsChanged(form(), form())).toBe(false);
  });

  it('ignora espacos e mascaras que nao mudam o valor', () => {
    const typed = withManager({ displayName: ' Marina ', phone: '47991234567' });

    expect(freeFieldsChanged(form(), typed)).toBe(false);
  });

  it.each([
    ['o nome de exibicao', withManager({ displayName: 'Mari' })],
    ['o celular', withManager({ phone: '(47) 98888-7766' })],
    [
      'a rua do sindico',
      withManager({ address: { ...form().manager.address, street: 'Rua Nova' } }),
    ],
    [
      'o complemento do sindico',
      withManager({ address: { ...form().manager.address, complement: '' } }),
    ],
    ['o nome do condominio', withCondominium({ name: 'Residencial Novo' })],
    [
      'a cidade do condominio',
      withCondominium({ address: { ...form().condominium.address, city: 'Joinville' } }),
    ],
    [
      'a UF do condominio',
      withCondominium({ address: { ...form().condominium.address, state: 'PR' } }),
    ],
  ])('e verdadeiro quando muda %s', (_name, changed) => {
    expect(freeFieldsChanged(form(), changed)).toBe(true);
  });

  it('nao conta mudanca no nome completo, na data nem no CPF', () => {
    const typed = withManager({
      fullName: 'Outra Pessoa',
      birthDate: '01/01/1990',
      cpf: '11144477735',
    });

    expect(freeFieldsChanged(form(), typed)).toBe(false);
  });
});

describe('identityChanged', () => {
  it('e falso quando nada mudou', () => {
    expect(identityChanged(form(), form())).toBe(false);
  });

  it('ignora espacos sobrando no nome', () => {
    expect(identityChanged(form(), withManager({ fullName: ' Marina  Souza ' }))).toBe(false);
  });

  it.each([
    ['o nome completo', withManager({ fullName: 'Marina Souza Lima' })],
    ['a data de nascimento', withManager({ birthDate: '16/06/1980' })],
    ['o CPF', withManager({ cpf: '111.444.777-35' })],
  ])('e verdadeiro quando muda %s', (_name, changed) => {
    expect(identityChanged(form(), changed)).toBe(true);
  });

  it('CPF em branco significa manter o atual, e nao e mudanca', () => {
    expect(identityChanged(form(), withManager({ cpf: '' }))).toBe(false);
  });

  it('nao conta mudanca nos campos livres', () => {
    const typed = withManager({ displayName: 'Mari', phone: '(47) 98888-7766' });

    expect(identityChanged(form(), typed)).toBe(false);
  });
});
