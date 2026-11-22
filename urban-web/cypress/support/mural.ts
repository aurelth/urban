export const MURAL_PATH = '/mural';

export type StubCategory = 'general' | 'maintenance' | 'important';

export interface StubMuralItem {
  id: string;
  title: string;
  body: string;
  category: StubCategory;
  scope: 'condominium' | 'block' | 'unit';
  block: string | null;
  unit: { id: string; block: string | null; number: string } | null;
  createdAt: string;
  updatedAt: string | null;
  read: boolean;
  readAt: string | null;
}

export function stubMuralItem(overrides: Partial<StubMuralItem> = {}): StubMuralItem {
  return {
    id: '0199cccc-0000-7000-8000-000000000001',
    title: 'Aviso',
    body: 'Texto do aviso.',
    category: 'general',
    scope: 'condominium',
    block: null,
    unit: null,
    createdAt: '2026-11-15T14:00:00Z',
    updatedAt: null,
    read: false,
    readAt: null,
    ...overrides,
  };
}

/** Com o relógio em `TODAY` (15/11/2026, 12:00 em Brasília), este é "há 1 h". */
export const MEETING = stubMuralItem({
  id: '0199cccc-0000-7000-8000-000000000001',
  title: 'Reunião de sábado',
  body: 'Às 10h no salão.\nTraga a sua opinião.',
  category: 'important',
});

/** "ontem", editado, só para o bloco A. */
export const WATER = stubMuralItem({
  id: '0199cccc-0000-7000-8000-000000000002',
  title: 'Água cortada na terça',
  body: 'Das 8h às 12h.',
  category: 'maintenance',
  scope: 'block',
  block: 'A',
  createdAt: '2026-11-14T12:00:00Z',
  updatedAt: '2026-11-14T15:30:00Z',
});

/** "há 3 dias", só para a unidade do morador. */
export const PARCEL = stubMuralItem({
  id: '0199cccc-0000-7000-8000-000000000003',
  title: 'Encomenda na portaria',
  category: 'general',
  scope: 'unit',
  unit: { id: '0199aaaa-0000-7000-8000-000000000101', block: 'A', number: '101' },
  createdAt: '2026-11-12T12:00:00Z',
});

/** Já lido, de mais de uma semana atrás: aparece com a data. */
export const POOL = stubMuralItem({
  id: '0199cccc-0000-7000-8000-000000000004',
  title: 'Horário da piscina',
  category: 'general',
  createdAt: '2026-11-01T12:00:00Z',
  read: true,
  readAt: '2026-11-02T13:00:00Z',
});

export const MURAL_ITEMS: StubMuralItem[] = [MEETING, WATER, PARCEL, POOL];

/** Avisos antigos e lidos para encher a segunda página. */
export function oldNotices(count: number): StubMuralItem[] {
  return Array.from({ length: count }, (_value, index) =>
    stubMuralItem({
      id: `0199cccc-0000-7000-8000-0000000001${String(index).padStart(2, '0')}`,
      title: `Aviso antigo ${String(index + 1)}`,
      createdAt: '2026-10-01T12:00:00Z',
      read: true,
      readAt: '2026-10-02T12:00:00Z',
    }),
  );
}

const MINE = /^\/api\/announcements\/mine(\?.*)?$/;
const READ_ONE = /^\/api\/announcements\/[0-9a-f-]+\/read$/;
const READ_ALL = /^\/api\/announcements\/read-all(\?.*)?$/;

function unreadCount(items: StubMuralItem[], category: StubCategory | null): number {
  return items.filter((item) => !item.read && (category === null || item.category === category))
    .length;
}

/**
 * A API do mural, com memória: marcar como lido muda a resposta seguinte, como na API de verdade. Filtros e página seguem
 * as regras dela (as contagens de não lidos ignoram os dois). O E2E do CI sobe só o front, sem API.
 */
export function stubMural(source: StubMuralItem[] = MURAL_ITEMS): void {
  const items = source.map((item) => ({ ...item }));
  const NOW_ISO = '2026-11-15T15:00:00Z';

  cy.intercept('GET', MINE, (request) => {
    const query = new URL(request.url, 'http://urban.test').searchParams;
    const category = query.get('category') as StubCategory | null;
    const unreadOnly = query.get('unreadOnly') === 'true';
    const page = Number(query.get('page') ?? '1');
    const pageSize = Number(query.get('pageSize') ?? '20');
    const matching = items.filter(
      (item) => (category === null || item.category === category) && (!unreadOnly || !item.read),
    );
    request.reply({
      statusCode: 200,
      body: {
        items: matching.slice((page - 1) * pageSize, page * pageSize),
        total: matching.length,
        unread: unreadCount(items, null),
        unreadByCategory: {
          general: unreadCount(items, 'general'),
          maintenance: unreadCount(items, 'maintenance'),
          important: unreadCount(items, 'important'),
        },
        page,
        pageSize,
      },
    });
  }).as('mural');

  cy.intercept('POST', READ_ONE, (request) => {
    const id = request.url.split('/').slice(-2)[0];
    const target = items.find((item) => item.id === id);
    if (target !== undefined) {
      target.read = true;
      target.readAt = NOW_ISO;
    }
    request.reply({ statusCode: 204 });
  }).as('markRead');

  cy.intercept('POST', READ_ALL, (request) => {
    const category = new URL(request.url, 'http://urban.test').searchParams.get('category');
    const pending = items.filter(
      (item) => !item.read && (category === null || item.category === category),
    );
    for (const item of pending) {
      item.read = true;
      item.readAt = NOW_ISO;
    }
    request.reply({ statusCode: 200, body: { marked: pending.length } });
  }).as('markAll');
}

/** A primeira busca do mural falha e as seguintes dão certo (o handler que não responde deixa a vez para `stubMural`). */
export function stubMuralFailingOnce(source: StubMuralItem[] = MURAL_ITEMS): void {
  stubMural(source);
  let failed = false;
  cy.intercept('GET', MINE, (request) => {
    if (failed) {
      return;
    }
    failed = true;
    request.reply({
      statusCode: 500,
      body: { status: 500, code: 'erro', detail: 'Erro' },
    });
  }).as('muralFailed');
}
