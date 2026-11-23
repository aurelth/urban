import { CurrentUser } from '../auth/auth.models';

/** O que a navegação precisa saber de quem está logado para decidir o que mostrar. */
type Who = Pick<CurrentUser, 'role' | 'condominiumId'> | null;

export interface NavItem {
  /** Vira o `data-testid` (`nav-<id>` no menu lateral) e a chave de `@for`. */
  readonly id: string;
  readonly label: string;
  /** Nome do ícone Material Symbols (precisa estar no glob de assets do angular.json). */
  readonly icon: string;
  readonly path: string;
  /** Marca o item que leva a bolinha com os avisos não lidos do mural. */
  readonly badge?: 'unread-announcements';
}

export interface NavGroup {
  readonly id: string;
  /** Título pequeno acima dos itens; `null` no grupo principal, que não precisa dele. */
  readonly label: string | null;
  readonly items: readonly NavItem[];
}

const DASHBOARD: NavItem = {
  id: 'dashboard',
  label: 'Painel',
  icon: 'dashboard',
  path: '/dashboard',
};

const MY_INVOICE: NavItem = {
  id: 'my-invoice',
  label: 'Minha fatura',
  icon: 'receipt_long',
  path: '/minha-fatura',
};

const MURAL: NavItem = {
  id: 'mural',
  label: 'Mural',
  icon: 'newspaper',
  path: '/mural',
  badge: 'unread-announcements',
};

/** O Painel do morador é o mural dele, então é nele que fica a bolinha dos avisos não lidos. */
const RESIDENT_DASHBOARD: NavItem = { ...DASHBOARD, badge: 'unread-announcements' };

const RESIDENT_GROUPS: readonly NavGroup[] = [
  { id: 'main', label: null, items: [RESIDENT_DASHBOARD, MY_INVOICE] },
];

const MANAGER_GROUPS: readonly NavGroup[] = [
  { id: 'main', label: null, items: [DASHBOARD] },
  {
    id: 'condominium',
    label: 'Condomínio',
    items: [
      { id: 'units', label: 'Unidades', icon: 'home_work', path: '/unidades' },
      { id: 'residents', label: 'Moradores', icon: 'groups', path: '/moradores' },
      { id: 'vehicles', label: 'Veículos', icon: 'directions_car', path: '/veiculos' },
      { id: 'announcements', label: 'Comunicados', icon: 'campaign', path: '/comunicados' },
    ],
  },
  {
    id: 'finance',
    label: 'Financeiro',
    items: [
      {
        id: 'month-dashboard',
        label: 'Painel financeiro',
        icon: 'monitoring',
        path: '/financeiro/painel',
      },
      { id: 'expenses', label: 'Despesas', icon: 'receipt_long', path: '/financeiro/despesas' },
      { id: 'tariffs', label: 'Tarifas', icon: 'price_change', path: '/financeiro/tarifas' },
      { id: 'readings', label: 'Leituras', icon: 'water_drop', path: '/financeiro/leituras' },
      {
        id: 'entries',
        label: 'Lançamentos',
        icon: 'calendar_month',
        path: '/financeiro/lancamentos',
      },
      {
        id: 'invoice-deliveries',
        label: 'Enviar faturas',
        icon: 'forward_to_inbox',
        path: '/financeiro/enviar-faturas',
      },
      { id: 'payments', label: 'Pagamentos', icon: 'payments', path: '/financeiro/pagamentos' },
      {
        id: 'delinquency',
        label: 'Inadimplência',
        icon: 'money_off',
        path: '/financeiro/inadimplencia',
      },
      {
        id: 'payment-instructions',
        label: 'Dados de pagamento',
        icon: 'account_balance',
        path: '/financeiro/dados-de-pagamento',
      },
    ],
  },
];

/**
 * O síndico que mora no condomínio também é morador: "Mural" e "Minha fatura" entram no grupo principal, logo depois do
 * Painel. Para o morador o Painel já é o mural; o painel do síndico é o de gestão, então o mural dele tem item próprio.
 */
const MANAGER_GROUPS_WHO_LIVES_HERE: readonly NavGroup[] = MANAGER_GROUPS.map((group) =>
  group.id === 'main' ? { ...group, items: [...group.items, MURAL, MY_INVOICE] } : group,
);

const OVERVIEW: NavItem = {
  id: 'overview',
  label: 'Visão geral',
  icon: 'dashboard',
  path: '/admin/visao-geral',
};

const ADMIN_GROUPS: readonly NavGroup[] = [
  { id: 'main', label: null, items: [OVERVIEW] },
  {
    id: 'admin',
    label: 'Administração',
    items: [
      { id: 'managers', label: 'Síndicos', icon: 'groups', path: '/admin/sindicos' },
      {
        id: 'registration-requests',
        label: 'Pedidos de cadastro',
        icon: 'how_to_reg',
        path: '/admin/pedidos-de-cadastro',
      },
      { id: 'messages', label: 'Mensagens', icon: 'mail', path: '/admin/mensagens' },
    ],
  },
];

const CONTACT: NavItem = {
  id: 'contact',
  label: 'Contato',
  icon: 'support_agent',
  path: '/contato',
};

const PROFILE: NavItem = {
  id: 'profile',
  label: 'Meus dados',
  icon: 'apartment',
  path: '/condominio/meus-dados',
};

/** O cadastro do morador, só para ler. O ícone é o do "Meus dados" do síndico: um ícone novo pede arquivo novo nos assets. */
const MY_DATA: NavItem = {
  id: 'my-data',
  label: 'Meus dados',
  icon: 'apartment',
  path: '/meus-dados',
};

const ACCOUNT: NavItem = {
  id: 'account',
  label: 'Minha conta',
  icon: 'account_circle',
  path: '/conta',
};

/** O síndico que já cadastrou o condomínio tem as telas de gestão; o administrador tem as de administração. */
function managesCondominium(who: Who): boolean {
  return who?.role === 'Manager' && who.condominiumId !== null;
}

/**
 * Itens do menu lateral, agrupados. Vazio quando a pessoa não tem telas (o menu nem aparece). `livesHere` diz que o
 * síndico mora no condomínio (ver `ManagerHousingStore`): só ele ganha "Minha fatura"; o morador já a tem sempre.
 */
export function navGroupsFor(who: Who, livesHere = false): readonly NavGroup[] {
  if (who?.role === 'SuperAdmin') {
    return ADMIN_GROUPS;
  }
  if (managesCondominium(who)) {
    return livesHere ? MANAGER_GROUPS_WHO_LIVES_HERE : MANAGER_GROUPS;
  }
  return who?.role === 'Resident' && who.condominiumId !== null ? RESIDENT_GROUPS : [];
}

/** Itens do canto inferior do menu lateral, separados dos grupos: hoje só o "Contato" do síndico com condomínio. */
export function navFooterFor(who: Who): readonly NavItem[] {
  return managesCondominium(who) ? [CONTACT] : [];
}

/**
 * Itens do menu do avatar, acima de "Sair": "Meus dados" é do síndico com condomínio (os dados dele e do condomínio) e do
 * morador (o cadastro dele, só para ler); "Minha conta" é de todos.
 */
export function userMenuFor(who: Who): readonly NavItem[] {
  if (managesCondominium(who)) {
    return [PROFILE, ACCOUNT];
  }
  return who?.role === 'Resident' && who.condominiumId !== null ? [MY_DATA, ACCOUNT] : [ACCOUNT];
}
