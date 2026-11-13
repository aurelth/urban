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

const MANAGER_GROUPS: readonly NavGroup[] = [
  { id: 'main', label: null, items: [DASHBOARD] },
  {
    id: 'condominium',
    label: 'Condomínio',
    items: [{ id: 'units', label: 'Unidades', icon: 'home_work', path: '/unidades' }],
  },
  {
    id: 'finance',
    label: 'Financeiro',
    items: [
      { id: 'expenses', label: 'Despesas', icon: 'receipt_long', path: '/financeiro/despesas' },
      { id: 'tariffs', label: 'Tarifas', icon: 'price_change', path: '/financeiro/tarifas' },
      { id: 'readings', label: 'Leituras', icon: 'water_drop', path: '/financeiro/leituras' },
      {
        id: 'entries',
        label: 'Lançamentos',
        icon: 'calendar_month',
        path: '/financeiro/lancamentos',
      },
    ],
  },
];

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

const ACCOUNT: NavItem = {
  id: 'account',
  label: 'Minha conta',
  icon: 'account_circle',
  path: '/conta',
};

/** O síndico que já cadastrou o condomínio tem as telas de gestão; o administrador tem as de administração; o morador ganha as suas depois. */
function managesCondominium(who: Who): boolean {
  return who?.role === 'Manager' && who.condominiumId !== null;
}

/** Itens do menu lateral, agrupados. Vazio quando a pessoa não tem telas de gestão (o menu nem aparece). */
export function navGroupsFor(who: Who): readonly NavGroup[] {
  if (who?.role === 'SuperAdmin') {
    return ADMIN_GROUPS;
  }
  return managesCondominium(who) ? MANAGER_GROUPS : [];
}

/** Itens do canto inferior do menu lateral, separados dos grupos: hoje só o "Contato" do síndico com condomínio. */
export function navFooterFor(who: Who): readonly NavItem[] {
  return managesCondominium(who) ? [CONTACT] : [];
}

/** Itens do menu do avatar, acima de "Sair": "Meus dados" é do síndico com condomínio, "Minha conta" é de todos. */
export function userMenuFor(who: Who): readonly NavItem[] {
  return managesCondominium(who) ? [PROFILE, ACCOUNT] : [ACCOUNT];
}
