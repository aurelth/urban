/** Situação da conta como a API devolve. */
export type ManagerStatus = 'Active' | 'Suspended' | 'Deactivated' | 'PendingVerification';

/** Filtro de situação da lista; vazio é "todos". Os valores são os que a API aceita, sem diferenciar maiúsculas. */
export type ManagerStatusFilter =
  '' | 'active' | 'suspended' | 'deactivated' | 'pendingVerification';

/** Síndico na lista do administrador. Nome e condomínio são nulos enquanto a pessoa não os informou. */
export interface Manager {
  readonly id: string;
  readonly email: string;
  readonly name: string | null;
  readonly condominiumId: string | null;
  readonly condominiumName: string | null;
  readonly status: ManagerStatus;
  /** Motivo da última suspensão; nulo quando a conta não está suspensa. */
  readonly statusReason: string | null;
  readonly statusChangedAt: string;
  readonly createdAt: string;
}

/** Resposta de GET /api/admin/managers. */
export interface ManagersPage {
  readonly items: readonly Manager[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}

/** Resposta de GET /api/admin/summary. */
export interface PlatformSummary {
  readonly activeManagers: number;
  readonly suspendedManagers: number;
  readonly awaitingConfirmationManagers: number;
  readonly pendingRequests: number;
  readonly condominiums: number;
}

export interface ManagersQuery {
  readonly status: ManagerStatusFilter;
  readonly search: string;
  readonly page: number;
  readonly pageSize: number;
}

export const DEFAULT_MANAGERS_QUERY: ManagersQuery = {
  status: '',
  search: '',
  page: 1,
  pageSize: 20,
};
