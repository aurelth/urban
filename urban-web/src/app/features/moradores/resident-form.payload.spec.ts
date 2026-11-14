import { resident } from '../../testing/residents.fixtures';
import {
  EMPTY_RESIDENT_FORM,
  ResidentFormValue,
  residentChanged,
  toCreatePayload,
  toResidentFormValue,
  toUpdatePayload,
} from './resident-form.payload';

const FILLED: ResidentFormValue = {
  fullName: '  Maria   Souza ',
  cpf: '529.982.247-25',
  birthDate: '20/05/1990',
  phone: '(47) 99876-5432',
  email: ' maria@exemplo.com ',
  profession: ' Engenheira ',
  contactName: 'Carlos  Souza',
  contactRelationship: ' Irmão',
  contactPhone: '(47) 3333-4444',
  unitId: '0199aaaa-0000-7000-8000-000000000001',
  kind: 'tenant',
  startDate: '01/03/2024',
};

describe('toResidentFormValue', () => {
  it('mostra os dados como aparecem nos campos: data em dd/mm/aaaa e telefones com máscara', () => {
    const value = toResidentFormValue(
      resident({
        fullName: 'Maria Souza',
        birthDate: '1990-05-20',
        phone: '47998765432',
        email: 'maria@exemplo.com',
        profession: 'Engenheira',
        emergencyContact: { name: 'Carlos Souza', relationship: 'Irmão', phone: '4733334444' },
      }),
    );

    expect(value).toEqual({
      ...EMPTY_RESIDENT_FORM,
      fullName: 'Maria Souza',
      birthDate: '20/05/1990',
      phone: '(47) 99876-5432',
      email: 'maria@exemplo.com',
      profession: 'Engenheira',
      contactName: 'Carlos Souza',
      contactRelationship: 'Irmão',
      contactPhone: '(47) 3333-4444',
    });
  });

  it('deixa o CPF em branco: o número salvo nunca chega à tela', () => {
    expect(toResidentFormValue(resident({ maskedCpf: '***.456.789-**' })).cpf).toBe('');
  });

  it('usa texto vazio para o que o morador não tem (e-mail, profissão, contato de emergência)', () => {
    const value = toResidentFormValue(
      resident({ email: null, profession: null, emergencyContact: null }),
    );

    expect(value.email).toBe('');
    expect(value.profession).toBe('');
    expect([value.contactName, value.contactRelationship, value.contactPhone]).toEqual([
      '',
      '',
      '',
    ]);
  });

  it('não preenche unidade, tipo de vínculo nem entrada: isso é só do cadastro', () => {
    const value = toResidentFormValue(resident());

    expect([value.unitId, value.kind, value.startDate]).toEqual(['', '', '']);
  });
});

describe('toCreatePayload', () => {
  it('manda só dígitos, a data em AAAA-MM-DD e os textos sem espaços sobrando', () => {
    expect(toCreatePayload(FILLED)).toEqual({
      fullName: 'Maria Souza',
      cpf: '52998224725',
      birthDate: '1990-05-20',
      phone: '47998765432',
      email: 'maria@exemplo.com',
      profession: 'Engenheira',
      emergencyContact: { name: 'Carlos Souza', relationship: 'Irmão', phone: '4733334444' },
      unitId: '0199aaaa-0000-7000-8000-000000000001',
      kind: 'tenant',
      startDate: '2024-03-01',
    });
  });

  it('manda o e-mail nulo quando ele fica em branco', () => {
    expect(toCreatePayload({ ...FILLED, email: '   ' }).email).toBeNull();
  });
});

describe('toUpdatePayload', () => {
  it('leva a versão lida e os mesmos campos do cadastro, sem unidade, tipo nem entrada', () => {
    expect(toUpdatePayload(FILLED, '7')).toEqual({
      version: '7',
      fullName: 'Maria Souza',
      cpf: '52998224725',
      birthDate: '1990-05-20',
      phone: '47998765432',
      email: 'maria@exemplo.com',
      profession: 'Engenheira',
      emergencyContact: { name: 'Carlos Souza', relationship: 'Irmão', phone: '4733334444' },
    });
  });

  it('não leva a chave do CPF quando ele está em branco, e a API mantém o atual', () => {
    const payload = toUpdatePayload({ ...FILLED, cpf: '' }, '7');

    expect(Object.keys(payload)).not.toContain('cpf');
  });

  it('manda o e-mail nulo quando ele foi apagado, para a API limpá-lo', () => {
    expect(toUpdatePayload({ ...FILLED, email: '' }, '7').email).toBeNull();
  });
});

describe('residentChanged', () => {
  const original = toResidentFormValue(resident());

  it('não vê mudança quando nada foi mexido', () => {
    expect(residentChanged(original, { ...original })).toBe(false);
  });

  it('não conta espaço sobrando nem máscara, porque compara o que iria para a API', () => {
    const current: ResidentFormValue = {
      ...original,
      fullName: ` ${original.fullName}  `,
      phone: original.phone.replace(/\D/g, ''),
    };

    expect(residentChanged(original, current)).toBe(false);
  });

  it.each([
    ['o nome', { fullName: 'Maria Souza Lima' }],
    ['a data de nascimento', { birthDate: '21/05/1990' }],
    ['o celular', { phone: '(47) 99999-0000' }],
    ['o e-mail', { email: 'outro@exemplo.com' }],
    ['a profissão', { profession: 'Médica' }],
    ['o nome do contato', { contactName: 'Paulo Souza' }],
    ['o parentesco', { contactRelationship: 'Pai' }],
    ['o telefone do contato', { contactPhone: '(47) 3333-0000' }],
  ])('vê mudança em %s', (_name, change) => {
    expect(residentChanged(original, { ...original, ...change })).toBe(true);
  });

  it('vê mudança quando um CPF novo é digitado', () => {
    expect(residentChanged(original, { ...original, cpf: '529.982.247-25' })).toBe(true);
  });

  it('vê mudança quando o e-mail é apagado', () => {
    expect(residentChanged(original, { ...original, email: '' })).toBe(true);
  });
});
