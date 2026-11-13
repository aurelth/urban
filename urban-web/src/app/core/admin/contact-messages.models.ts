/** Mensagem do síndico ao dono da plataforma (item de GET /api/admin/contact-messages). */
export interface ContactMessage {
  readonly id: string;
  readonly senderId: string;
  readonly senderEmail: string;
  /** Nulo enquanto o síndico não disse como prefere ser chamado. */
  readonly senderName: string | null;
  /** Nulo enquanto o síndico não cadastrou o condomínio. */
  readonly condominiumName: string | null;
  readonly subject: string;
  readonly body: string;
  readonly sentAt: string;
  /** Primeira leitura pelo administrador; nulo enquanto não lida. */
  readonly readAt: string | null;
}

/** Resposta de GET /api/admin/contact-messages. */
export interface ContactMessagesPage {
  readonly items: readonly ContactMessage[];
  readonly total: number;
  readonly page: number;
  readonly pageSize: number;
}

/** Filtro da caixa de entrada; vazio é "todas". Os valores são os que a API aceita. */
export type ContactMessagesFilter = '' | 'unread';

export interface ContactMessagesQuery {
  readonly status: ContactMessagesFilter;
  readonly page: number;
  readonly pageSize: number;
}

export const DEFAULT_CONTACT_MESSAGES_QUERY: ContactMessagesQuery = {
  status: '',
  page: 1,
  pageSize: 20,
};
