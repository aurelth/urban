import { InvoiceDeliveryState } from '../../core/finance/invoice-deliveries.models';
import {
  deliveryAction,
  deliveryFailureText,
  deliveryStateView,
  pendingCount,
  sendSummary,
} from './invoice-delivery-values';

describe('deliveryStateView', () => {
  it.each([
    ['sent', 'Enviada', 'check_circle', 'text-success'],
    ['queued', 'Na fila', 'schedule', 'text-warning'],
    ['failed', 'Falhou', 'error', 'text-error'],
    ['notSent', 'Não enviada', 'mail', 'text-on-surface-variant'],
    ['noRecipient', 'Sem e-mail', 'warning', 'text-warning'],
  ] as const)('%s: texto, ícone e cor', (state, label, icon, tone) => {
    expect(deliveryStateView(state)).toEqual({ label, icon, tone });
  });
});

describe('deliveryAction', () => {
  it.each([
    ['notSent', 'Enviar'],
    ['failed', 'Enviar de novo'],
    ['sent', 'Reenviar'],
  ] as const)('%s oferece "%s"', (state, label) => {
    expect(deliveryAction(state)).toBe(label);
  });

  it.each(['queued', 'noRecipient'] as const satisfies readonly InvoiceDeliveryState[])(
    '%s não oferece ação',
    (state) => {
      expect(deliveryAction(state)).toBeNull();
    },
  );
});

describe('deliveryFailureText', () => {
  it.each([
    ['not_delivered', 'O provedor de e-mail recusou o envio'],
    ['no_recipient', 'O responsável não tinha e-mail cadastrado'],
    ['invoice_unavailable', 'Não foi possível montar a fatura'],
    ['outra_coisa', 'Não foi possível enviar'],
    [null, 'Não foi possível enviar'],
  ])('%s vira "%s"', (reason, text) => {
    expect(deliveryFailureText(reason)).toBe(text);
  });
});

describe('pendingCount', () => {
  it('soma o que o envio do mês mandaria: as não enviadas e as que falharam', () => {
    expect(pendingCount({ noRecipient: 4, notSent: 3, queued: 2, sent: 7, failed: 1 })).toBe(4);
  });

  it('é zero quando tudo foi enviado, está na fila ou não tem e-mail', () => {
    expect(pendingCount({ noRecipient: 1, notSent: 0, queued: 1, sent: 5, failed: 0 })).toBe(0);
  });
});

describe('sendSummary', () => {
  it('conta o que entrou na fila', () => {
    expect(sendSummary({ queued: 3, alreadySent: 0, alreadyQueued: 0, withoutRecipient: 0 })).toBe(
      '3 faturas entraram na fila de envio',
    );
  });

  it('usa o singular para uma fatura', () => {
    expect(sendSummary({ queued: 1, alreadySent: 0, alreadyQueued: 0, withoutRecipient: 0 })).toBe(
      '1 fatura entrou na fila de envio',
    );
  });

  it('diz o que ficou de fora, uma frase por motivo', () => {
    expect(sendSummary({ queued: 2, alreadySent: 5, alreadyQueued: 1, withoutRecipient: 3 })).toBe(
      '2 faturas entraram na fila de envio. 5 unidades já tinham recebido a fatura. ' +
        '1 fatura já estava na fila. 3 unidades ficaram de fora por não terem responsável com e-mail',
    );
  });

  it('usa o singular nos motivos', () => {
    expect(sendSummary({ queued: 1, alreadySent: 1, alreadyQueued: 0, withoutRecipient: 1 })).toBe(
      '1 fatura entrou na fila de envio. 1 unidade já tinha recebido a fatura. ' +
        '1 unidade ficou de fora por não ter responsável com e-mail',
    );
  });

  it('avisa quando não há nada novo para enviar', () => {
    expect(sendSummary({ queued: 0, alreadySent: 4, alreadyQueued: 0, withoutRecipient: 0 })).toBe(
      'Nenhuma fatura nova para enviar. 4 unidades já tinham recebido a fatura',
    );
  });
});
