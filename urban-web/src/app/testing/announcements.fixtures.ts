import { Announcement, AnnouncementPage } from '../core/announcements/announcements.models';

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
