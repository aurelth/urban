import { CurrentUser } from '../auth/auth.models';
import { navFooterFor, navGroupsFor, userMenuFor } from './nav-items';

type Who = Pick<CurrentUser, 'role' | 'condominiumId'>;

const MANAGER_WITH_CONDOMINIUM: Who = { role: 'Manager', condominiumId: 'c-1' };
const ADMIN: Who = { role: 'SuperAdmin', condominiumId: null };
const RESIDENT_WITH_CONDOMINIUM: Who = { role: 'Resident', condominiumId: 'c-1' };

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
      ['units', 'residents', 'vehicles'],
      [
        'month-dashboard',
        'expenses',
        'tariffs',
        'readings',
        'entries',
        'invoice-deliveries',
        'payments',
        'delinquency',
        'payment-instructions',
      ],
    ]);
  });

  it('leva cada item à sua tela, com texto e ícone', () => {
    const items = navGroupsFor(MANAGER_WITH_CONDOMINIUM).flatMap((group) => group.items);

    expect(items.map((item) => [item.label, item.path, item.icon])).toEqual([
      ['Painel', '/dashboard', 'dashboard'],
      ['Unidades', '/unidades', 'home_work'],
      ['Moradores', '/moradores', 'groups'],
      ['Veículos', '/veiculos', 'directions_car'],
      ['Painel financeiro', '/financeiro/painel', 'monitoring'],
      ['Despesas', '/financeiro/despesas', 'receipt_long'],
      ['Tarifas', '/financeiro/tarifas', 'price_change'],
      ['Leituras', '/financeiro/leituras', 'water_drop'],
      ['Lançamentos', '/financeiro/lancamentos', 'calendar_month'],
      ['Enviar faturas', '/financeiro/enviar-faturas', 'forward_to_inbox'],
      ['Pagamentos', '/financeiro/pagamentos', 'payments'],
      ['Inadimplência', '/financeiro/inadimplencia', 'money_off'],
      ['Dados de pagamento', '/financeiro/dados-de-pagamento', 'account_balance'],
    ]);
  });

  it.each([
    ['ninguém logado', null],
    ['síndico sem condomínio', { role: 'Manager', condominiumId: null } as Who],
    ['morador sem condomínio', { role: 'Resident', condominiumId: null } as Who],
  ])('não tem menu lateral para %s', (_name, who) => {
    expect(navGroupsFor(who)).toEqual([]);
  });

  it('leva o morador ao painel e à sua fatura, sem as telas de gestão', () => {
    const groups = navGroupsFor(RESIDENT_WITH_CONDOMINIUM);

    expect(groups.map((group) => [group.id, group.label])).toEqual([['main', null]]);
    expect(
      groups
        .flatMap((group) => group.items)
        .map((item) => [item.id, item.label, item.path, item.icon]),
    ).toEqual([
      ['dashboard', 'Painel', '/dashboard', 'dashboard'],
      ['my-invoice', 'Minha fatura', '/minha-fatura', 'receipt_long'],
    ]);
  });

  it('leva o administrador à visão geral, aos síndicos e aos pedidos de cadastro, sem as telas de condomínio', () => {
    const groups = navGroupsFor(ADMIN);

    expect(groups.map((group) => [group.id, group.label])).toEqual([
      ['main', null],
      ['admin', 'Administração'],
    ]);
    expect(
      groups
        .flatMap((group) => group.items)
        .map((item) => [item.id, item.label, item.path, item.icon]),
    ).toEqual([
      ['overview', 'Visão geral', '/admin/visao-geral', 'dashboard'],
      ['managers', 'Síndicos', '/admin/sindicos', 'groups'],
      ['registration-requests', 'Pedidos de cadastro', '/admin/pedidos-de-cadastro', 'how_to_reg'],
      ['messages', 'Mensagens', '/admin/mensagens', 'mail'],
    ]);
  });
});

describe('síndico que mora no condomínio', () => {
  it('ganha "Minha fatura" logo depois do Painel, no grupo principal', () => {
    const groups = navGroupsFor(MANAGER_WITH_CONDOMINIUM, true);

    expect(groups.map((group) => group.id)).toEqual(['main', 'condominium', 'finance']);
    expect(groups[0].items.map((item) => [item.id, item.label, item.path, item.icon])).toEqual([
      ['dashboard', 'Painel', '/dashboard', 'dashboard'],
      ['my-invoice', 'Minha fatura', '/minha-fatura', 'receipt_long'],
    ]);
  });

  it('não muda o resto do menu do síndico', () => {
    const withInvoice = navGroupsFor(MANAGER_WITH_CONDOMINIUM, true).slice(1);
    const without = navGroupsFor(MANAGER_WITH_CONDOMINIUM, false).slice(1);

    expect(withInvoice).toEqual(without);
  });

  it('não ganha o item quando não mora: é o menu de sempre', () => {
    expect(navGroupsFor(MANAGER_WITH_CONDOMINIUM, false)[0].items.map((item) => item.id)).toEqual([
      'dashboard',
    ]);
    expect(navGroupsFor(MANAGER_WITH_CONDOMINIUM)[0].items.map((item) => item.id)).toEqual([
      'dashboard',
    ]);
  });

  it('não dá item a quem não é síndico com condomínio, mesmo com o aviso de que mora', () => {
    expect(navGroupsFor({ role: 'Manager', condominiumId: null }, true)).toEqual([]);
    expect(navGroupsFor(null, true)).toEqual([]);
    expect(navGroupsFor(ADMIN, true)).toEqual(navGroupsFor(ADMIN, false));
    expect(navGroupsFor(RESIDENT_WITH_CONDOMINIUM, true)).toEqual(
      navGroupsFor(RESIDENT_WITH_CONDOMINIUM, false),
    );
  });
});

describe('navFooterFor', () => {
  it('leva o síndico com condomínio ao Contato, no canto inferior do menu', () => {
    expect(
      navFooterFor(MANAGER_WITH_CONDOMINIUM).map((item) => [
        item.id,
        item.label,
        item.path,
        item.icon,
      ]),
    ).toEqual([['contact', 'Contato', '/contato', 'support_agent']]);
  });

  it.each([
    ['ninguém logado', null],
    ['síndico sem condomínio', { role: 'Manager', condominiumId: null } as Who],
    ['morador', { role: 'Resident', condominiumId: 'c-1' } as Who],
    ['administrador', ADMIN],
  ])('não tem item no canto inferior para %s', (_name, who) => {
    expect(navFooterFor(who)).toEqual([]);
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
