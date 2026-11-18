export const INVOICE_DELIVERIES_PATH = '/financeiro/enviar-faturas';

export function invoiceDeliveriesApi(month = '2026-11'): string {
  return `/api/finance/months/${month}/invoice-deliveries`;
}

export type StubDeliveryState = 'noRecipient' | 'notSent' | 'queued' | 'sent' | 'failed';

export interface StubDelivery {
  chargeId: string;
  unitId: string;
  block: string | null;
  number: string;
  responsibleName: string | null;
  state: StubDeliveryState;
  sentAt: string | null;
  failureReason: string | null;
  /** Quantas leituras da lista faltam para a fatura na fila ser entregue (a lista viva troca "Na fila" por "Enviada"). */
  readsUntilSent?: number;
}

export const DELIVERY_101: StubDelivery = {
  chargeId: '0199bbbb-0030-7000-8000-000000000101',
  unitId: '0199bbbb-0005-7000-8000-000000000101',
  block: 'A',
  number: '101',
  responsibleName: 'Maria Souza',
  state: 'notSent',
  sentAt: null,
  failureReason: null,
};

export const DELIVERY_102: StubDelivery = {
  chargeId: '0199bbbb-0030-7000-8000-000000000102',
  unitId: '0199bbbb-0005-7000-8000-000000000102',
  block: 'A',
  number: '102',
  responsibleName: 'João Lima',
  state: 'failed',
  sentAt: null,
  failureReason: 'not_delivered',
};

export const DELIVERY_201: StubDelivery = {
  chargeId: '0199bbbb-0030-7000-8000-000000000201',
  unitId: '0199bbbb-0005-7000-8000-000000000201',
  block: null,
  number: '201',
  responsibleName: 'Paulo Reis',
  state: 'sent',
  sentAt: '2026-11-12T13:30:00Z',
  failureReason: null,
};

export const DELIVERY_301: StubDelivery = {
  chargeId: '0199bbbb-0030-7000-8000-000000000301',
  unitId: '0199bbbb-0005-7000-8000-000000000301',
  block: 'B',
  number: '301',
  responsibleName: 'Carlos Dias',
  state: 'noRecipient',
  sentAt: null,
  failureReason: null,
};

const DELIVERED_AT = '2026-11-15T15:00:00Z';

/**
 * Os envios do mês (GET), com as contagens e a paginação que a API faz. Devolve a lista viva: o envio do mês e o reenvio
 * (`stubSendMonth`, `stubResend`) põem as faturas na fila, e cada leitura seguinte aproxima a entrega, como o Hangfire faria.
 */
export function stubInvoiceDeliveries(
  initial: readonly StubDelivery[] = [DELIVERY_101, DELIVERY_102, DELIVERY_201, DELIVERY_301],
  month = '2026-11',
): StubDelivery[] {
  const deliveries = initial.map((delivery) => ({ ...delivery }));
  cy.intercept({ method: 'GET', pathname: invoiceDeliveriesApi(month) }, (request) => {
    const params = new URL(request.url).searchParams;
    const page = Number(params.get('page') ?? '1');
    const pageSize = Number(params.get('pageSize') ?? '50');
    const counts = { noRecipient: 0, notSent: 0, queued: 0, sent: 0, failed: 0 };
    for (const delivery of deliveries) {
      counts[delivery.state] += 1;
    }
    const items = deliveries
      .slice((page - 1) * pageSize, page * pageSize)
      .map((delivery) => ({ ...delivery, readsUntilSent: undefined }));
    request.reply({
      statusCode: 200,
      body: { month, total: deliveries.length, counts, page, pageSize, items },
    });
    for (const delivery of deliveries) {
      if (delivery.state === 'queued' && delivery.readsUntilSent !== undefined) {
        delivery.readsUntilSent -= 1;
        if (delivery.readsUntilSent <= 0) {
          delivery.state = 'sent';
          delivery.sentAt = DELIVERED_AT;
        }
      }
    }
  }).as('deliveries');
  return deliveries;
}

/** O mês ainda não foi fechado: a API recusa a lista com 409. */
export function stubInvoiceDeliveriesNotClosed(month = '2026-11'): void {
  cy.intercept(
    { method: 'GET', pathname: invoiceDeliveriesApi(month) },
    {
      statusCode: 409,
      body: { status: 409, code: 'closing.not_closed', detail: 'Detalhe da API' },
    },
  ).as('deliveries');
}

/**
 * O envio das faturas do mês (POST): põe na fila as não enviadas e as que falharam (menos as sem e-mail) e responde com o
 * resumo. A entrega vem duas leituras depois, para a tela mostrar "Na fila" e então "Enviada" sozinha.
 */
export function stubSendMonth(deliveries: StubDelivery[], month = '2026-11'): void {
  cy.intercept({ method: 'POST', pathname: invoiceDeliveriesApi(month) }, (request) => {
    const summary = { queued: 0, alreadySent: 0, alreadyQueued: 0, withoutRecipient: 0 };
    for (const delivery of deliveries) {
      if (delivery.state === 'notSent' || delivery.state === 'failed') {
        delivery.state = 'queued';
        delivery.failureReason = null;
        delivery.readsUntilSent = 2;
        summary.queued += 1;
      } else if (delivery.state === 'sent') {
        summary.alreadySent += 1;
      } else if (delivery.state === 'queued') {
        summary.alreadyQueued += 1;
      } else {
        summary.withoutRecipient += 1;
      }
    }
    request.reply({ statusCode: 202, body: summary });
  }).as('sendMonth');
}

/** O reenvio da fatura de uma cobrança (POST): põe só essa na fila. */
export function stubResend(deliveries: StubDelivery[]): void {
  cy.intercept('POST', /\/api\/finance\/charges\/[^/]+\/invoice-deliveries$/, (request) => {
    const chargeId = request.url.split('/').at(-2)!;
    const delivery = deliveries.find((candidate) => candidate.chargeId === chargeId)!;
    delivery.state = 'queued';
    delivery.failureReason = null;
    delivery.readsUntilSent = 2;
    request.reply({ statusCode: 202 });
  }).as('resend');
}

/** O reenvio recusado pela API, com o código que ela manda. */
export function stubResendRejected(status: number, code: string): void {
  cy.intercept('POST', /\/api\/finance\/charges\/[^/]+\/invoice-deliveries$/, {
    statusCode: status,
    body: { status, code, detail: 'Detalhe da API' },
  }).as('resend');
}
