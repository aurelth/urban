/** Arquivo anexado a uma resposta. A API guarda só estes dados: o arquivo foi direto ao e-mail e não fica no Urban. */
export interface ContactReplyAttachment {
  readonly fileName: string;
  readonly contentType: string;
  /** Tamanho em bytes. */
  readonly size: number;
}

/** Resposta do administrador a uma mensagem, enviada por e-mail ao síndico. */
export interface ContactReply {
  readonly id: string;
  readonly sentAt: string;
  readonly body: string;
  readonly attachments: readonly ContactReplyAttachment[];
}

/** Resposta de POST /api/admin/contact-messages/{id}/replies. */
export interface ContactReplyReceipt {
  readonly id: string;
  readonly sentAt: string;
  readonly attachments: readonly ContactReplyAttachment[];
}

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
  /** Respostas já enviadas, da mais antiga à mais nova. */
  readonly replies: readonly ContactReply[];
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
