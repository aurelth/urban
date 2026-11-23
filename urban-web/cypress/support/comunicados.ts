import { FIRST_UNIT, SECOND_UNIT } from './unidades';

export const ANNOUNCEMENTS_PATH = '/comunicados';

export interface StubAnnouncement {
  id: string;
  title: string;
  body: string;
  category: 'general' | 'maintenance' | 'important';
  scope: 'condominium' | 'block' | 'unit';
  block: string | null;
  unitId: string | null;
  createdAt: string;
  updatedAt: string | null;
  recipients: number;
  readers: number;
  notifiedByEmail: boolean;
  version: string;
}

export function stubAnnouncement(overrides: Partial<StubAnnouncement> = {}): StubAnnouncement {
  return {
    id: '0199bbbb-0000-7000-8000-000000000001',
    title: 'Reunião de sábado',
    body: 'Às 10h no salão.\nTraga a sua opinião.',
    category: 'important',
    scope: 'condominium',
    block: null,
    unitId: null,
    createdAt: '2026-11-22T13:50:00Z',
    updatedAt: null,
    recipients: 40,
    readers: 12,
    notifiedByEmail: true,
    version: '31',
    ...overrides,
  };
}

export const FIRST_ANNOUNCEMENT = stubAnnouncement();
export const SECOND_ANNOUNCEMENT = stubAnnouncement({
  id: '0199bbbb-0000-7000-8000-000000000002',
  title: 'Água cortada',
  body: 'Na terça, das 8h às 12h.',
  category: 'maintenance',
  scope: 'block',
  block: 'A',
  createdAt: '2026-11-20T12:00:00Z',
  updatedAt: '2026-11-21T15:30:00Z',
  recipients: 1,
  readers: 1,
  notifiedByEmail: false,
  version: '32',
});
export const THIRD_ANNOUNCEMENT = stubAnnouncement({
  id: '0199bbbb-0000-7000-8000-000000000003',
  title: 'Encomenda na portaria',
  category: 'general',
  scope: 'unit',
  unitId: SECOND_UNIT.id,
  recipients: 0,
  readers: 0,
  notifiedByEmail: false,
  version: '33',
});

export const ANNOUNCEMENT_UNIT = FIRST_UNIT;

const LIST_URL = /^\/api\/announcements(\?.*)?$/;

/** A lista de comunicados; o E2E do CI sobe só o front, sem API. As unidades vêm de `stubUnits`. */
export function stubAnnouncements(
  items: StubAnnouncement[] = [FIRST_ANNOUNCEMENT, SECOND_ANNOUNCEMENT, THIRD_ANNOUNCEMENT],
  alias = 'announcements',
): void {
  cy.intercept('GET', LIST_URL, {
    statusCode: 200,
    body: { items, total: items.length, page: 1, pageSize: 20 },
  }).as(alias);
}

export function stubAnnouncementRejected(
  method: 'POST' | 'PUT' | 'DELETE',
  url: string | RegExp,
  statusCode: number,
  code: string,
  alias: string,
): void {
  cy.intercept(method, url, {
    statusCode,
    body: { status: statusCode, code, detail: 'Detalhe da API' },
  }).as(alias);
}
