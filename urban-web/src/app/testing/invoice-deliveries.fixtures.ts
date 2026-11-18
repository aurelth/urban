import {
  InvoiceDelivery,
  InvoiceDeliveryState,
  MonthInvoiceDeliveries,
} from '../core/finance/invoice-deliveries.models';

let sequence = 0;

function nextId(prefix: string): string {
  sequence += 1;
  return `0199bbbb-${prefix}-7000-8000-${String(sequence).padStart(12, '0')}`;
}

export function invoiceDelivery(overrides: Partial<InvoiceDelivery> = {}): InvoiceDelivery {
  return {
    chargeId: nextId('0040'),
    unitId: nextId('0041'),
    block: 'A',
    number: '101',
    responsibleName: 'Maria Souza',
    state: 'notSent',
    sentAt: null,
    failureReason: null,
    ...overrides,
  };
}

/** Sem `counts`, conta as situações dos itens (do mês inteiro, que nos testes cabe numa página só). */
export function monthInvoiceDeliveries(
  overrides: Partial<MonthInvoiceDeliveries> = {},
): MonthInvoiceDeliveries {
  const items = overrides.items ?? [invoiceDelivery()];
  const counts: Record<InvoiceDeliveryState, number> = {
    noRecipient: 0,
    notSent: 0,
    queued: 0,
    sent: 0,
    failed: 0,
  };
  for (const item of items) {
    counts[item.state] += 1;
  }
  return {
    month: '2026-11',
    total: items.length,
    counts,
    page: 1,
    pageSize: 50,
    items,
    ...overrides,
  };
}
