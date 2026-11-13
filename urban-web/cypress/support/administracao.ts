export const OVERVIEW_PATH = '/admin/visao-geral';
export const MANAGERS_PATH = '/admin/sindicos';
const SUMMARY_URL = '/api/admin/summary';
const MANAGERS_LIST = /\/api\/admin\/managers(\?.*)?$/;
const MANAGER_ACTION = /\/api\/admin\/managers\/[^/?]+\/(suspend|reactivate)$/;

type Status = 'Active' | 'Suspended' | 'Deactivated' | 'PendingVerification';

export interface StubManager {
  id: string;
  email: string;
  name: string | null;
  condominiumId: string | null;
  condominiumName: string | null;
  status: Status;
  statusReason: string | null;
  statusChangedAt: string;
  createdAt: string;
}

export interface StubSummary {
  activeManagers: number;
  suspendedManagers: number;
  awaitingConfirmationManagers: number;
  pendingRequests: number;
  condominiums: number;
}

export const SUMMARY: StubSummary = {
  activeManagers: 12,
  suspendedManagers: 2,
  awaitingConfirmationManagers: 3,
  pendingRequests: 4,
  condominiums: 11,
};

export const ANA: StubManager = {
  id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c5001',
  email: 'ana@example.com',
  name: 'Ana',
  condominiumId: 'c-1',
  condominiumName: 'Residencial Aurora',
  status: 'Active',
  statusReason: null,
  statusChangedAt: '2026-11-10T12:00:00+00:00',
  createdAt: '2026-11-01T15:30:00+00:00',
};

export const CARLOS: StubManager = {
  ...ANA,
  id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c5002',
  email: 'carlos@example.com',
  name: 'Carlos',
  condominiumId: 'c-2',
  condominiumName: 'Edifício Bela Vista',
  status: 'Suspended',
  statusReason: 'Uso indevido da plataforma',
  statusChangedAt: '2026-11-12T13:05:00+00:00',
};

export const PAULA: StubManager = {
  ...ANA,
  id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c5003',
  email: 'paula@example.com',
  name: null,
  condominiumId: null,
  condominiumName: null,
  status: 'PendingVerification',
};

/** Mais síndicos do que cabem numa página de 20, para a paginação. */
export function manyManagers(count: number): StubManager[] {
  return Array.from({ length: count }, (_, index) => ({
    ...ANA,
    id: `0198f3c1-6a4e-7c20-9d52-${String(index).padStart(12, '0')}`,
    email: `sindico${index + 1}@example.com`,
    name: `Síndico ${String(index + 1).padStart(2, '0')}`,
  }));
}

export function stubSummary(summary: StubSummary = SUMMARY): void {
  cy.intercept('GET', SUMMARY_URL, { statusCode: 200, body: summary }).as('summary');
}

function matches(manager: StubManager, status: string, search: string): boolean {
  const statusOk = status === '' || manager.status.toLowerCase() === status.toLowerCase();
  const term = search.trim().toLowerCase();
  const searchOk =
    term === '' ||
    manager.email.toLowerCase().includes(term) ||
    (manager.name ?? '').toLowerCase().includes(term);
  return statusOk && searchOk;
}

/**
 * A API simulada guarda os síndicos: filtra por situação e busca, pagina, e suspender ou reativar muda a situação, de
 * modo que a lista seguinte já vem atualizada.
 */
export function stubManagers(initial: StubManager[] = [ANA, CARLOS]): void {
  const managers = initial.map((manager) => ({ ...manager }));
  stubSummary();
  cy.intercept('GET', MANAGERS_LIST, (request) => {
    const url = new URL(request.url);
    const status = url.searchParams.get('status') ?? '';
    const search = url.searchParams.get('search') ?? '';
    const page = Number(url.searchParams.get('page') ?? '1');
    const pageSize = Number(url.searchParams.get('pageSize') ?? '20');
    const found = managers.filter((manager) => matches(manager, status, search));
    request.reply({
      statusCode: 200,
      body: {
        items: found.slice((page - 1) * pageSize, page * pageSize),
        total: found.length,
        page,
        pageSize,
      },
    });
  }).as('managers');
  cy.intercept('POST', MANAGER_ACTION, (request) => {
    const [id, action] = request.url.split('/').slice(-2);
    const target = managers.find((manager) => manager.id === id)!;
    target.status = action === 'suspend' ? 'Suspended' : 'Active';
    target.statusReason = action === 'suspend' ? request.body.reason : null;
    request.reply({ statusCode: 204 });
  }).as('change');
}

/** A API recusa suspender ou reativar (a situação já mudou, o síndico sumiu...); a lista seguinte vem como `remaining`. */
export function stubChangeRejected(
  statusCode: number,
  code: string,
  remaining: StubManager[],
): void {
  stubSummary();
  cy.intercept('GET', MANAGERS_LIST, {
    statusCode: 200,
    body: { items: remaining, total: remaining.length, page: 1, pageSize: 20 },
  }).as('managers');
  cy.intercept('POST', MANAGER_ACTION, {
    statusCode,
    body: { status: statusCode, code, detail: 'Detalhe da API' },
  }).as('change');
}
