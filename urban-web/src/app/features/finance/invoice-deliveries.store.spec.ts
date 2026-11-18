import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { invoiceDelivery, monthInvoiceDeliveries } from '../../testing/invoice-deliveries.fixtures';
import { InvoiceDeliveriesStore } from './invoice-deliveries.store';

const LIST = (month: string): string => `/api/finance/months/${month}/invoice-deliveries`;

type Store = InstanceType<typeof InvoiceDeliveriesStore>;

function setup(): { store: Store; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), InvoiceDeliveriesStore],
  });
  return {
    store: TestBed.inject(InvoiceDeliveriesStore),
    http: TestBed.inject(HttpTestingController),
  };
}

function listRequest(
  http: HttpTestingController,
  params: Record<string, string | null> = {},
  month = '2026-11',
): { flush: (body: unknown, init?: { status: number; statusText: string }) => void } {
  const request = http.expectOne((r) => r.method === 'GET' && r.url === LIST(month));
  for (const [name, value] of Object.entries(params)) {
    expect(request.request.params.get(name)).toBe(value);
  }
  return request;
}

async function shown(
  store: Store,
  http: HttpTestingController,
  data = monthInvoiceDeliveries(),
): Promise<void> {
  const showing = store.show('2026-11');
  listRequest(http).flush(data);
  await showing;
}

function fail(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

const ACCEPTED = { status: 202, statusText: 'Accepted' };

describe('InvoiceDeliveriesStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('comeca carregando, sem mes, sem dados e na primeira pagina', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.month()).toBe('');
    expect(store.data()).toBeNull();
    expect(store.busy()).toBe(false);
    expect(store.query()).toEqual({ page: 1, pageSize: 50 });
    expect(store.pending()).toBe(0);
    expect(store.waiting()).toBe(false);
  });

  describe('show', () => {
    it('le o mes e fica pronto', async () => {
      const { store, http } = setup();

      const showing = store.show('2026-11');
      expect(store.status()).toBe('loading');
      expect(store.month()).toBe('2026-11');
      listRequest(http, { page: '1', pageSize: '50' }).flush(monthInvoiceDeliveries());
      await showing;

      expect(store.status()).toBe('ready');
      expect(store.data()?.items).toHaveLength(1);
    });

    it('vira "nao fechado" quando a API diz que o mes ainda nao foi fechado', async () => {
      const { store, http } = setup();

      const showing = store.show('2026-11');
      listRequest(http).flush(...fail(409, 'closing.not_closed'));
      await showing;

      expect(store.status()).toBe('not-closed');
      expect(store.data()).toBeNull();
    });

    it('vira "erro" nas outras falhas e pode tentar de novo', async () => {
      const { store, http } = setup();
      const first = store.show('2026-11');
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await first;
      expect(store.status()).toBe('error');

      await shown(store, http);

      expect(store.status()).toBe('ready');
    });

    it('troca de mes voltando para a primeira pagina', async () => {
      const { store, http } = setup();
      await shown(store, http, monthInvoiceDeliveries({ total: 120 }));
      const paging = store.goToPage(2);
      listRequest(http, { page: '2' }).flush(monthInvoiceDeliveries({ page: 2, total: 120 }));
      await paging;

      const showing = store.show('2026-12');
      listRequest(http, { page: '1' }, '2026-12').flush(
        monthInvoiceDeliveries({ month: '2026-12' }),
      );
      await showing;

      expect(store.month()).toBe('2026-12');
      expect(store.query().page).toBe(1);
    });
  });

  describe('goToPage', () => {
    it('busca a pagina pedida e mantem a lista antiga enquanto busca', async () => {
      const { store, http } = setup();
      await shown(store, http, monthInvoiceDeliveries({ total: 120 }));

      const paging = store.goToPage(2);
      expect(store.busy()).toBe(true);
      expect(store.status()).toBe('ready');
      listRequest(http, { page: '2', pageSize: '50' }).flush(
        monthInvoiceDeliveries({ page: 2, total: 120 }),
      );
      await paging;

      expect(store.busy()).toBe(false);
      expect(store.query().page).toBe(2);
      expect(store.pageCount()).toBe(3);
    });

    it.each([0, 4, 1.5, Number.NaN])('ignora a pagina %s, fora dos limites', async (page) => {
      const { store, http } = setup();
      await shown(store, http, monthInvoiceDeliveries({ total: 120 }));

      await store.goToPage(page);

      http.expectNone(() => true);
      expect(store.query().page).toBe(1);
    });
  });

  describe('contagens', () => {
    const items = [
      invoiceDelivery({ state: 'notSent' }),
      invoiceDelivery({ state: 'failed' }),
      invoiceDelivery({ state: 'queued' }),
      invoiceDelivery({ state: 'sent' }),
      invoiceDelivery({ state: 'noRecipient' }),
    ];

    it('"pending" e o que o envio do mes mandaria: as nao enviadas e as que falharam', async () => {
      const { store, http } = setup();

      await shown(store, http, monthInvoiceDeliveries({ items }));

      expect(store.pending()).toBe(2);
    });

    it('"waiting" so vale com fatura na fila', async () => {
      const { store, http } = setup();
      await shown(store, http, monthInvoiceDeliveries({ items }));
      expect(store.waiting()).toBe(true);

      const showing = store.show('2026-11');
      listRequest(http).flush(
        monthInvoiceDeliveries({ items: [invoiceDelivery({ state: 'sent' })] }),
      );
      await showing;

      expect(store.waiting()).toBe(false);
    });

    it('a contagem de paginas nunca e menor que 1', async () => {
      const { store, http } = setup();
      await shown(store, http, monthInvoiceDeliveries({ items: [], total: 0 }));

      expect(store.pageCount()).toBe(1);
    });
  });

  describe('sendMonth', () => {
    it('manda o mes, devolve o resumo e le a lista de novo', async () => {
      const { store, http } = setup();
      await shown(store, http);

      const sending = store.sendMonth();
      expect(store.busy()).toBe(true);
      const post = http.expectOne((r) => r.method === 'POST' && r.url === LIST('2026-11'));
      const summary = { queued: 1, alreadySent: 0, alreadyQueued: 0, withoutRecipient: 0 };
      post.flush(summary, ACCEPTED);
      await flushPromises();
      listRequest(http).flush(
        monthInvoiceDeliveries({ items: [invoiceDelivery({ state: 'queued' })] }),
      );
      const outcome = await sending;

      expect(outcome).toEqual({ result: summary, problem: null });
      expect(store.busy()).toBe(false);
      expect(store.data()?.counts.queued).toBe(1);
    });

    it('com o mes ainda nao fechado, devolve o problema e leitura vira "nao fechado"', async () => {
      const { store, http } = setup();
      await shown(store, http);

      const sending = store.sendMonth();
      http.expectOne((r) => r.method === 'POST').flush(...fail(409, 'closing.not_closed'));
      await flushPromises();
      listRequest(http).flush(...fail(409, 'closing.not_closed'));
      const outcome = await sending;

      expect(outcome.result).toBeNull();
      expect(outcome.problem?.code).toBe('closing.not_closed');
      expect(store.status()).toBe('not-closed');
    });

    it('no limite de requisicoes devolve o problema e nao le a lista de novo', async () => {
      const { store, http } = setup();
      await shown(store, http);

      const sending = store.sendMonth();
      http.expectOne((r) => r.method === 'POST').flush(...fail(429, 'rate_limit.exceeded'));
      const outcome = await sending;

      expect(outcome.problem?.status).toBe(429);
      http.expectNone((r) => r.method === 'GET');
      expect(store.status()).toBe('ready');
      expect(store.busy()).toBe(false);
    });
  });

  describe('resend', () => {
    it('reenvia a fatura de uma cobranca e le a lista de novo', async () => {
      const { store, http } = setup();
      await shown(store, http);

      const resending = store.resend('c1');
      http
        .expectOne(
          (r) => r.method === 'POST' && r.url === '/api/finance/charges/c1/invoice-deliveries',
        )
        .flush(null, ACCEPTED);
      await flushPromises();
      listRequest(http).flush(monthInvoiceDeliveries());

      expect(await resending).toBeNull();
      expect(store.busy()).toBe(false);
    });

    it.each([
      [404, 'finance.charge.not_found'],
      [409, 'finance.invoice_delivery.already_queued'],
      [422, 'finance.invoice_delivery.no_recipient'],
    ])(
      'recusa %s (%s): devolve o problema e le a lista de novo, porque a tela estava velha',
      async (status, code) => {
        const { store, http } = setup();
        await shown(store, http);

        const resending = store.resend('c1');
        http.expectOne((r) => r.method === 'POST').flush(...fail(status, code));
        await flushPromises();
        listRequest(http).flush(monthInvoiceDeliveries());
        const result = await resending;

        expect(result?.code).toBe(code);
      },
    );

    it('no limite de requisicoes devolve o problema sem ler a lista de novo', async () => {
      const { store, http } = setup();
      await shown(store, http);

      const resending = store.resend('c1');
      http.expectOne((r) => r.method === 'POST').flush(...fail(429, 'rate_limit.exceeded'));
      const result = await resending;

      expect(result?.status).toBe(429);
      http.expectNone((r) => r.method === 'GET');
    });
  });

  describe('refresh e poll', () => {
    it('refresh le de novo a pagina atual, sem trocar a lista por "carregando"', async () => {
      const { store, http } = setup();
      await shown(store, http);

      const refreshing = store.refresh();
      expect(store.busy()).toBe(true);
      expect(store.status()).toBe('ready');
      listRequest(http).flush(
        monthInvoiceDeliveries({ items: [invoiceDelivery({ state: 'sent' })] }),
      );
      await refreshing;

      expect(store.busy()).toBe(false);
      expect(store.data()?.counts.sent).toBe(1);
    });

    it('se a pagina ficou vazia, volta uma', async () => {
      const { store, http } = setup();
      await shown(store, http, monthInvoiceDeliveries({ total: 60 }));
      const paging = store.goToPage(2);
      listRequest(http, { page: '2' }).flush(monthInvoiceDeliveries({ page: 2, total: 60 }));
      await paging;

      const refreshing = store.refresh();
      listRequest(http, { page: '2' }).flush(
        monthInvoiceDeliveries({ page: 2, items: [], total: 50 }),
      );
      await flushPromises();
      listRequest(http, { page: '1' }).flush(monthInvoiceDeliveries({ total: 50 }));
      await refreshing;

      expect(store.query().page).toBe(1);
    });

    it('poll le sem marcar ocupado', async () => {
      const { store, http } = setup();
      await shown(
        store,
        http,
        monthInvoiceDeliveries({ items: [invoiceDelivery({ state: 'queued' })] }),
      );

      const polling = store.poll();
      expect(store.busy()).toBe(false);
      listRequest(http).flush(
        monthInvoiceDeliveries({ items: [invoiceDelivery({ state: 'sent' })] }),
      );
      await polling;

      expect(store.waiting()).toBe(false);
    });

    it('poll que falha mantem o que ja estava na tela', async () => {
      const { store, http } = setup();
      await shown(
        store,
        http,
        monthInvoiceDeliveries({ items: [invoiceDelivery({ state: 'queued' })] }),
      );

      const polling = store.poll();
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await polling;

      expect(store.status()).toBe('ready');
      expect(store.waiting()).toBe(true);
    });

    it('poll que chega depois da troca de mes e descartado', async () => {
      const { store, http } = setup();
      await shown(
        store,
        http,
        monthInvoiceDeliveries({ items: [invoiceDelivery({ state: 'queued' })] }),
      );

      const polling = store.poll();
      const stale = listRequest(http);
      const showing = store.show('2026-12');
      const fresh = listRequest(http, {}, '2026-12');
      fresh.flush(
        monthInvoiceDeliveries({ month: '2026-12', items: [invoiceDelivery({ state: 'sent' })] }),
      );
      await showing;
      stale.flush(monthInvoiceDeliveries({ items: [invoiceDelivery({ state: 'queued' })] }));
      await polling;

      expect(store.month()).toBe('2026-12');
      expect(store.data()?.month).toBe('2026-12');
    });

    it('poll nao faz nada enquanto ha outra chamada em andamento', async () => {
      const { store, http } = setup();
      await shown(store, http);
      const resending = store.resend('c1');
      http.expectOne((r) => r.method === 'POST').flush(null, ACCEPTED);
      await flushPromises();
      const refresh = listRequest(http);

      await store.poll();

      http.expectNone((r) => r.method === 'GET');
      refresh.flush(monthInvoiceDeliveries());
      await resending;
    });
  });
});
