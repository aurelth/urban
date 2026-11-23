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
  /** O síndico pediu o e-mail ao publicar: o aviso foi mandado às contas de quem mora no alcance. */
  readonly notifiedByEmail: boolean;
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
  /** Só ao publicar: pede também o e-mail a quem mora no alcance. Na edição não vai, porque o e-mail sai uma vez só. */
  readonly notifyByEmail?: boolean;
}

/** Corpo de PUT /api/announcements/{id}: os mesmos campos, mais a versão lida. */
export interface UpdateAnnouncementPayload extends AnnouncementPayload {
  readonly version: string;
}

/** Quantos avisos o mural do morador mostra por vez; "Mostrar mais avisos" busca o resto. */
export const MURAL_PAGE_SIZE = 10;

/** A unidade a que um aviso se destina, como o mural a mostra. O bloco é nulo em condomínio sem blocos. */
export interface MyAnnouncementUnit {
  readonly id: string;
  readonly block: string | null;
  readonly number: string;
}

/**
 * Aviso como a API o devolve a quem mora: sem o autor nem quem leu, mas com a leitura da própria conta. `block` só vem no
 * alcance de bloco e `unit` só no de unidade.
 */
export interface MyAnnouncement {
  readonly id: string;
  readonly title: string;
  readonly body: string;
  readonly category: AnnouncementCategory;
  readonly scope: AnnouncementScope;
  readonly block: string | null;
  readonly unit: MyAnnouncementUnit | null;
  readonly createdAt: string;
  readonly updatedAt: string | null;
  readonly read: boolean;
  readonly readAt: string | null;
}

/** Não lidos por categoria, no mural inteiro: nem o filtro nem a página mudam essas contagens. */
export interface UnreadByCategory {
  readonly general: number;
  readonly maintenance: number;
  readonly important: number;
}

/** Resposta de GET /api/announcements/mine. `total` acompanha os filtros; `unread` e `unreadByCategory`, não. */
export interface MyAnnouncementPage {
  readonly items: readonly MyAnnouncement[];
  readonly total: number;
  readonly unread: number;
  readonly unreadByCategory: UnreadByCategory;
  readonly page: number;
  readonly pageSize: number;
}

/** O que o mural pede à API: a página, o tamanho e os dois filtros (que valem juntos). */
export interface MyAnnouncementsQuery {
  readonly page: number;
  readonly pageSize: number;
  readonly unreadOnly: boolean;
  readonly category: AnnouncementCategory | null;
}

/** Resposta de POST /api/announcements/read-all: quantos avisos passaram a lidos. */
export interface MarkAllResult {
  readonly marked: number;
}
