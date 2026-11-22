export type AnnouncementCategory = 'general' | 'maintenance' | 'important';

/** A quem o comunicado se dirige: o condomínio inteiro, um bloco ou uma unidade. */
export type AnnouncementScope = 'condominium' | 'block' | 'unit';

export const ANNOUNCEMENT_TITLE_MAX = 120;
export const ANNOUNCEMENT_BODY_MAX = 5000;

/** Quantos comunicados a lista do síndico mostra por página. */
export const ANNOUNCEMENTS_PAGE_SIZE = 20;

/**
 * Comunicado como a API o devolve ao síndico. `recipients` são as contas de quem mora hoje no alcance (sem o próprio
 * síndico), `readers` as que já marcaram como lido, e a versão volta em toda alteração.
 */
export interface Announcement {
  readonly id: string;
  readonly title: string;
  readonly body: string;
  readonly category: AnnouncementCategory;
  readonly scope: AnnouncementScope;
  readonly block: string | null;
  readonly unitId: string | null;
  readonly createdAt: string;
  readonly updatedAt: string | null;
  readonly recipients: number;
  readonly readers: number;
  readonly version: string;
}

/** Resposta de GET /api/announcements. */
export interface AnnouncementPage {
  readonly items: readonly Announcement[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}

/** Corpo de POST /api/announcements. Bloco só no alcance de bloco e unidade só no de unidade; no resto, nulos. */
export interface AnnouncementPayload {
  readonly title: string;
  readonly body: string;
  readonly category: AnnouncementCategory;
  readonly scope: AnnouncementScope;
  readonly block: string | null;
  readonly unitId: string | null;
}

/** Corpo de PUT /api/announcements/{id}: os mesmos campos, mais a versão lida. */
export interface UpdateAnnouncementPayload extends AnnouncementPayload {
  readonly version: string;
}
