import { Manager, ManagersPage, PlatformSummary } from '../core/admin/admin.models';

export const PLATFORM_SUMMARY_URL = '/api/admin/summary';
export const MANAGERS_URL = '/api/admin/managers';

export function platformSummary(overrides: Partial<PlatformSummary> = {}): PlatformSummary {
  return {
    activeManagers: 12,
    suspendedManagers: 2,
    awaitingConfirmationManagers: 3,
    pendingRequests: 4,
    condominiums: 11,
    ...overrides,
  };
}

export function manager(overrides: Partial<Manager> = {}): Manager {
  return {
    id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c5001',
    email: 'ana@example.com',
    name: 'Ana',
    condominiumId: 'c-1',
    condominiumName: 'Residencial Aurora',
    status: 'Active',
    statusReason: null,
    statusChangedAt: '2026-11-10T12:00:00+00:00',
    createdAt: '2026-11-01T15:30:00+00:00',
    ...overrides,
  };
}

export const SUSPENDED_MANAGER: Manager = manager({
  id: '0198f3c1-6a4e-7c20-9d52-3f7a1b2c5002',
  email: 'carlos@example.com',
  name: 'Carlos',
  condominiumId: 'c-2',
  condominiumName: 'Edifício Bela Vista',
  status: 'Suspended',
  statusReason: 'Uso indevido da plataforma',
  statusChangedAt: '2026-11-12T13:05:00+00:00',
});

export function managersPage(
  items: Manager[],
  total = items.length,
  page = 1,
  pageSize = 20,
): ManagersPage {
  return { items, total, page, pageSize };
}
