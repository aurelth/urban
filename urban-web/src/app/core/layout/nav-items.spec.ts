import { CurrentUser } from '../auth/auth.models';
import { navGroupsFor, userMenuFor } from './nav-items';

type Who = Pick<CurrentUser, 'role' | 'condominiumId'>;

const MANAGER_WITH_CONDOMINIUM: Who = { role: 'Manager', condominiumId: 'c-1' };

describe('navGroupsFor', () => {
  it('agrupa os itens do síndico com condomínio: painel, condomínio e financeiro', () => {
    const groups = navGroupsFor(MANAGER_WITH_CONDOMINIUM);

    expect(groups.map((group) => [group.id, group.label])).toEqual([
      ['main', null],
      ['condominium', 'Condomínio'],
      ['finance', 'Financeiro'],
    ]);
    expect(groups.map((group) => group.items.map((item) => item.id))).toEqual([
      ['dashboard'],
      ['units'],
      ['expenses', 'tariffs', 'readings', 'entries'],
    ]);
  });

  it('leva cada item à sua tela, com texto e ícone', () => {
    const items = navGroupsFor(MANAGER_WITH_CONDOMINIUM).flatMap((group) => group.items);

    expect(items.map((item) => [item.label, item.path, item.icon])).toEqual([
      ['Painel', '/dashboard', 'dashboard'],
      ['Unidades', '/unidades', 'home_work'],
      ['Despesas', '/financeiro/despesas', 'receipt_long'],
      ['Tarifas', '/financeiro/tarifas', 'price_change'],
      ['Leituras', '/financeiro/leituras', 'water_drop'],
      ['Lançamentos', '/financeiro/lancamentos', 'calendar_month'],
    ]);
  });

  it.each([
    ['ninguém logado', null],
    ['síndico sem condomínio', { role: 'Manager', condominiumId: null } as Who],
    ['morador', { role: 'Resident', condominiumId: 'c-1' } as Who],
    ['administrador', { role: 'SuperAdmin', condominiumId: null } as Who],
  ])('não tem menu lateral para %s', (_name, who) => {
    expect(navGroupsFor(who)).toEqual([]);
  });
});

describe('userMenuFor', () => {
  it('leva o síndico com condomínio aos dados e à conta', () => {
    expect(
      userMenuFor(MANAGER_WITH_CONDOMINIUM).map((item) => [item.id, item.label, item.path]),
    ).toEqual([
      ['profile', 'Meus dados', '/condominio/meus-dados'],
      ['account', 'Minha conta', '/conta'],
    ]);
  });

  it.each([
    ['síndico sem condomínio', { role: 'Manager', condominiumId: null } as Who],
    ['morador', { role: 'Resident', condominiumId: 'c-1' } as Who],
    ['administrador', { role: 'SuperAdmin', condominiumId: null } as Who],
  ])('leva só à conta para %s', (_name, who) => {
    expect(userMenuFor(who).map((item) => item.id)).toEqual(['account']);
  });
});
