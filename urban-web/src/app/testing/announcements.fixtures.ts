import {
  Announcement,
  AnnouncementPage,
  MURAL_PAGE_SIZE,
  MyAnnouncement,
  MyAnnouncementPage,
  UnreadByCategory,
} from '../core/announcements/announcements.models';

let sequence = 0;

/** Comunicado de teste para o condomínio inteiro; cada chamada ganha um id novo, e o título acompanha. */
export function announcement(overrides: Partial<Announcement> = {}): Announcement {
  sequence += 1;
  return {
    id: `0199bbbb-0000-7000-8000-${String(sequence).padStart(12, '0')}`,
    title: `Aviso ${sequence}`,
    body: 'Texto do aviso.',
    category: 'general',
    scope: 'condominium',
    block: null,
    unitId: null,
    createdAt: '2026-11-22T13:50:00Z',
    updatedAt: null,
    recipients: 10,
    readers: 3,
    version: String(sequence),
    ...overrides,
  };
}

export function announcementPage(
  items: Announcement[],
  total = items.length,
  page = 1,
  pageSize = 20,
): AnnouncementPage {
  return { items, total, page, pageSize };
}

/** Aviso do mural do morador, para o condomínio inteiro e ainda não lido. */
export function myAnnouncement(overrides: Partial<MyAnnouncement> = {}): MyAnnouncement {
  sequence += 1;
  return {
    id: `0199cccc-0000-7000-8000-${String(sequence).padStart(12, '0')}`,
    title: `Aviso ${sequence}`,
    body: 'Texto do aviso.',
    category: 'general',
    scope: 'condominium',
    block: null,
    unit: null,
    createdAt: '2026-11-22T13:50:00Z',
    updatedAt: null,
    read: false,
    readAt: null,
    ...overrides,
  };
}

export const NOTHING_UNREAD: UnreadByCategory = { general: 0, maintenance: 0, important: 0 };

export function myAnnouncementPage(
  items: MyAnnouncement[],
  overrides: Partial<MyAnnouncementPage> = {},
): MyAnnouncementPage {
  return {
    items,
    total: items.length,
    unread: 0,
    unreadByCategory: NOTHING_UNREAD,
    page: 1,
    pageSize: MURAL_PAGE_SIZE,
    ...overrides,
  };
}
