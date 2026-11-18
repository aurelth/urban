/**
 * Situação do envio da fatura de uma cobrança, calculada pela API a cada leitura:
 * - noRecipient: a unidade não tem responsável com e-mail, então não há para quem enviar;
 * - notSent: ainda não foi enviada;
 * - queued: está na fila de envio (ou o envio está sendo feito);
 * - sent: o provedor de e-mail aceitou a mensagem;
 * - failed: o envio falhou depois de todas as tentativas.
 */
export type InvoiceDeliveryState = 'noRecipient' | 'notSent' | 'queued' | 'sent' | 'failed';

/** Uma unidade do mês fechado, com a situação do envio da fatura dela. */
export interface InvoiceDelivery {
  readonly chargeId: string;
  readonly unitId: string;
  readonly block: string | null;
  readonly number: string;
  /** Nome do responsável pela unidade; nulo quando ela não tem responsável. */
  readonly responsibleName: string | null;
  readonly state: InvoiceDeliveryState;
  /** Instante (ISO 8601) em que a fatura foi enviada; só existe quando `state` é `sent`. */
  readonly sentAt: string | null;
  /** Código do motivo da falha; só existe quando `state` é `failed`. */
  readonly failureReason: string | null;
}

/** Quantas unidades do mês inteiro estão em cada situação, não só as da página. */
export interface InvoiceDeliveryCounts {
  readonly noRecipient: number;
  readonly notSent: number;
  readonly queued: number;
  readonly sent: number;
  readonly failed: number;
}

export interface MonthInvoiceDeliveries {
  readonly month: string;
  readonly total: number;
  readonly counts: InvoiceDeliveryCounts;
  readonly page: number;
  readonly pageSize: number;
  readonly items: readonly InvoiceDelivery[];
}

/** O que o envio das faturas do mês fez: quantas entraram na fila e por que as outras ficaram de fora. */
export interface SendMonthInvoicesResult {
  readonly queued: number;
  readonly alreadySent: number;
  readonly alreadyQueued: number;
  readonly withoutRecipient: number;
}

export interface InvoiceDeliveriesQuery {
  readonly page: number;
  readonly pageSize: number;
}

export const DEFAULT_INVOICE_DELIVERIES_QUERY: InvoiceDeliveriesQuery = { page: 1, pageSize: 50 };
