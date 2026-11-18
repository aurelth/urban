import {
  InvoiceDeliveryCounts,
  InvoiceDeliveryState,
  SendMonthInvoicesResult,
} from '../../core/finance/invoice-deliveries.models';

export interface DeliveryStateView {
  readonly label: string;
  readonly icon: string;
  /** Classe de cor do estado: laranja em andamento ou atenção, verde sucesso, vermelho erro. */
  readonly tone: string;
}

const STATE_VIEWS: Readonly<Record<InvoiceDeliveryState, DeliveryStateView>> = {
  sent: { label: 'Enviada', icon: 'check_circle', tone: 'text-success' },
  queued: { label: 'Na fila', icon: 'schedule', tone: 'text-warning' },
  failed: { label: 'Falhou', icon: 'error', tone: 'text-error' },
  notSent: { label: 'Não enviada', icon: 'mail', tone: 'text-on-surface-variant' },
  noRecipient: { label: 'Sem e-mail', icon: 'warning', tone: 'text-warning' },
};

/** Texto, ícone e cor de cada situação: o estado nunca depende só da cor. */
export function deliveryStateView(state: InvoiceDeliveryState): DeliveryStateView {
  return STATE_VIEWS[state];
}

/** O texto do botão de cada linha; nulo quando não há o que fazer (na fila, ou sem e-mail para mandar). */
export function deliveryAction(state: InvoiceDeliveryState): string | null {
  switch (state) {
    case 'notSent':
      return 'Enviar';
    case 'failed':
      return 'Enviar de novo';
    case 'sent':
      return 'Reenviar';
    default:
      return null;
  }
}

const FAILURE_TEXTS: Readonly<Record<string, string>> = {
  not_delivered: 'O provedor de e-mail recusou o envio',
  no_recipient: 'O responsável não tinha e-mail cadastrado',
  invoice_unavailable: 'Não foi possível montar a fatura',
};

/** Por que o envio falhou, no texto da tela; um motivo que a tela não conhece vira a frase genérica. */
export function deliveryFailureText(reason: string | null): string {
  return reason !== null && Object.hasOwn(FAILURE_TEXTS, reason)
    ? FAILURE_TEXTS[reason]
    : 'Não foi possível enviar';
}

/** O que o envio das faturas do mês mandaria: as que ainda não foram enviadas e as que falharam. */
export function pendingCount(counts: InvoiceDeliveryCounts): number {
  return counts.notSent + counts.failed;
}

function plural(count: number, one: string, many: string): string {
  return `${count} ${count === 1 ? one : many}`;
}

/** Conta o que o envio do mês fez, uma frase por assunto. */
export function sendSummary(result: SendMonthInvoicesResult): string {
  const parts = [
    result.queued > 0
      ? `${plural(result.queued, 'fatura entrou', 'faturas entraram')} na fila de envio`
      : 'Nenhuma fatura nova para enviar',
  ];
  if (result.alreadySent > 0) {
    parts.push(
      `${plural(result.alreadySent, 'unidade já tinha', 'unidades já tinham')} recebido a fatura`,
    );
  }
  if (result.alreadyQueued > 0) {
    parts.push(`${plural(result.alreadyQueued, 'fatura já estava', 'faturas já estavam')} na fila`);
  }
  if (result.withoutRecipient > 0) {
    parts.push(
      `${plural(result.withoutRecipient, 'unidade ficou', 'unidades ficaram')} de fora por não ` +
        `${result.withoutRecipient === 1 ? 'ter' : 'terem'} responsável com e-mail`,
    );
  }
  return parts.join('. ');
}
