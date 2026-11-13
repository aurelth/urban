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

/** Só o síndico que já cadastrou o condomínio tem as telas de gestão; os demais perfis ganham as suas depois. */
function managesCondominium(who: Who): boolean {
  return who?.role === 'Manager' && who.condominiumId !== null;
}

/** Itens do menu lateral, agrupados. Vazio quando a pessoa não tem telas de gestão (o menu nem aparece). */
export function navGroupsFor(who: Who): readonly NavGroup[] {
  return managesCondominium(who) ? MANAGER_GROUPS : [];
}

/** Itens do menu do avatar, acima de "Sair": "Meus dados" é do síndico com condomínio, "Minha conta" é de todos. */
export function userMenuFor(who: Who): readonly NavItem[] {
  return managesCondominium(who) ? [PROFILE, ACCOUNT] : [ACCOUNT];
}
