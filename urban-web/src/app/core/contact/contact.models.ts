/** Resposta de POST /api/contact/messages: a mensagem foi registrada. */
export interface ContactReceipt {
  readonly id: string;
  /** Quando a mensagem foi registrada (ISO 8601). */
  readonly sentAt: string;
}
