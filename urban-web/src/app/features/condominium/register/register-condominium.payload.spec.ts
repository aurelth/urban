import { toRegisterPayload } from './register-condominium.payload';
import type { RegisterCondominiumFormValue } from './register-condominium.payload';

const FORM: RegisterCondominiumFormValue = {
  manager: {
    fullName: '  Marina   Souza ',
    displayName: ' Marina ',
    cpf: '529.982.247-25',
    birthDate: '15/06/1980',
    phone: '(47) 99123-4567',
    address: {
      postalCode: '89010-000',
      street: ' Rua das Flores ',
      number: ' 120 ',
      complement: ' Apto 42 ',
      neighborhood: ' Centro ',
      city: ' Blumenau ',
      state: 'SC',
    },
  },
  condominium: {
    name: ' Residencial das Flores ',
    cnpj: '11.222.333/0001-81',
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
};

describe('toRegisterPayload', () => {
  it('manda só dígitos, a data em aaaa-mm-dd e os textos sem espaços sobrando', () => {
    expect(toRegisterPayload(FORM, false)).toEqual({
      displayName: 'Marina',
      manager: {
        fullName: 'Marina Souza',
        cpf: '52998224725',
        birthDate: '1980-06-15',
        phone: '47991234567',
        address: {
          postalCode: '89010000',
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
        cnpj: '11222333000181',
        address: {
          postalCode: '89012100',
          street: 'Rua XV de Novembro',
          number: '500',
          complement: null,
          neighborhood: 'Centro',
          city: 'Blumenau',
          state: 'SC',
        },
      },
    });
  });

  it('manda o complemento como nulo quando só tem espaços', () => {
    const form = {
      ...FORM,
      manager: { ...FORM.manager, address: { ...FORM.manager.address, complement: '   ' } },
    };

    expect(toRegisterPayload(form, false).manager.address.complement).toBeNull();
  });

  it('usa o endereço do síndico também para o condomínio quando é o mesmo', () => {
    const payload = toRegisterPayload(FORM, true);

    expect(payload.condominium.address).toEqual(payload.manager.address);
    expect(payload.condominium.address.postalCode).toBe('89010000');
    expect(payload.condominium.address.complement).toBe('Apto 42');
  });

  it('mantém o CNPJ alfanumérico em maiúsculas', () => {
    const form = { ...FORM, condominium: { ...FORM.condominium, cnpj: '12.abc.345/01de-35' } };

    expect(toRegisterPayload(form, false).condominium.cnpj).toBe('12ABC34501DE35');
  });
});
