import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { chargeSummary, monthPayments } from '../../testing/finance.fixtures';
import { PaymentsStore } from './payments.store';

function setup(): { store: InstanceType<typeof PaymentsStore>; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), PaymentsStore],
  });
  return { store: TestBed.inject(PaymentsStore), http: TestBed.inject(HttpTestingController) };
}

function expectList(
  http: HttpTestingController,
  params: Record<string, string | null> = {},
  month = '2026-11',
): { flush: (body: unknown, init?: { status: number; statusText: string }) => void } {
  const request = http.expectOne((r) => r.url === `/api/finance/months/${month}/payments`);
  for (const [name, value] of Object.entries(params)) {
    expect(request.request.params.get(name)).toBe(value);
  }
  return request;
}

async function shown(
  store: InstanceType<typeof PaymentsStore>,
  http: HttpTestingController,
  data = monthPayments(),
): Promise<void> {
  const showing = store.show('2026-11');
  expectList(http).flush(data);
  await showing;
}

function fail(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

describe('PaymentsStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('comeca carregando, sem mes, sem dados e na primeira pagina de todas as cobrancas', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.month()).toBe('');
    expect(store.data()).toBeNull();
    expect(store.busy()).toBe(false);
    expect(store.query()).toEqual({ status: 'all', page: 1, pageSize: 50 });
  });

  describe('show', () => {
    it('le o mes e fica pronto', async () => {
      const { store, http } = setup();

      const showing = store.show('2026-11');
      expect(store.status()).toBe('loading');
      expect(store.month()).toBe('2026-11');
      expectList(http, { page: '1', pageSize: '50' }).flush(monthPayments({ received: 300 }));
      await showing;

      expect(store.status()).toBe('ready');
      expect(store.data()?.received).toBe(300);
    });

    it('vira "nao fechado" quando a API diz que o mes ainda nao foi fechado', async () => {
      const { store, http } = setup();

      const showing = store.show('2026-11');
      expectList(http).flush(...fail(409, 'closing.not_closed'));
      await showing;

      expect(store.status()).toBe('not-closed');
      expect(store.data()).toBeNull();
    });

    it('vira "erro" nas outras falhas e pode tentar de novo', async () => {
      const { store, http } = setup();
      const first = store.show('2026-11');
      expectList(http).flush(null, { status: 500, statusText: 'Erro' });
      await first;
      expect(store.status()).toBe('error');

      await shown(store, http);

      expect(store.status()).toBe('ready');
    });

    it('troca de mes mantendo a situacao escolhida e voltando para a primeira pagina', async () => {
      const { store, http } = setup();
      await shown(store, http, monthPayments({ total: 120 }));
      const filtering = store.filter('overdue');
      expectList(http, { status: 'overdue' }).flush(monthPayments({ total: 120 }));
      await filtering;
      const paging = store.goToPage(2);
      expectList(http, { page: '2' }).flush(monthPayments({ page: 2, total: 120 }));
      await paging;

      const showing = store.show('2026-12');
      expectList(http, { status: 'overdue', page: '1' }, '2026-12').flush(
        monthPayments({ month: '2026-12' }),
      );
      await showing;

      expect(store.month()).toBe('2026-12');
      expect(store.query()).toEqual({ status: 'overdue', page: 1, pageSize: 50 });
    });
  });

  describe('filter', () => {
    it('busca de novo pela situacao, na primeira pagina, e mantem a lista antiga enquanto busca', async () => {
      const { store, http } = setup();
      await shown(store, http);

      const filtering = store.filter('paid');
      expect(store.busy()).toBe(true);
      expect(store.status()).toBe('ready');
      expectList(http, { status: 'paid', page: '1' }).flush(
        monthPayments({ items: [chargeSummary({ status: 'paid' })] }),
      );
      await filtering;

      expect(store.busy()).toBe(false);
      expect(store.query().status).toBe('paid');
      expect(store.data()?.items[0].status).toBe('paid');
      expect(store.hasFilter()).toBe(true);
    });

    it('voltar para "todas" tira o filtro', async () => {
      const { store, http } = setup();
      await shown(store, http);
      const first = store.filter('paid');
      expectList(http).flush(monthPayments());
      await first;

      const second = store.filter('all');
      expectList(http, { status: null }).flush(monthPayments());
      await second;

      expect(store.hasFilter()).toBe(false);
    });

    it('vira "erro" quando a busca falha', async () => {
      const { store, http } = setup();
      await shown(store, http);

      const filtering = store.filter('open');
      expectList(http).flush(null, { status: 500, statusText: 'Erro' });
      await filtering;

      expect(store.status()).toBe('error');
      expect(store.busy()).toBe(false);
    });
  });

  describe('paginacao', () => {
    it('conta as paginas pelo total de cobrancas do filtro', async () => {
      const { store, http } = setup();
      await shown(store, http, monthPayments({ total: 120 }));

      expect(store.pageCount()).toBe(3);
    });

    it('tem ao menos uma pagina mesmo vazio', async () => {
      const { store, http } = setup();
      await shown(store, http, monthPayments({ items: [], total: 0 }));

      expect(store.pageCount()).toBe(1);
    });

    it('vai para a pagina pedida', async () => {
      const { store, http } = setup();
      await shown(store, http, monthPayments({ total: 120 }));

      const paging = store.goToPage(2);
      expectList(http, { page: '2' }).flush(monthPayments({ page: 2, total: 120 }));
      await paging;

      expect(store.query().page).toBe(2);
    });

    it.each([0, 4, 1.5, -1])('ignora a pagina %s, fora dos limites', async (page) => {
      const { store, http } = setup();
      await shown(store, http, monthPayments({ total: 120 }));

      await store.goToPage(page);

      expect(store.query().page).toBe(1);
    });
  });

  describe('refresh', () => {
    it('le de novo o mes, com a mesma situacao e pagina, sem voltar para "carregando"', async () => {
      const { store, http } = setup();
      await shown(store, http, monthPayments({ total: 120 }));
      const paging = store.goToPage(2);
      expectList(http).flush(monthPayments({ page: 2, total: 120 }));
      await paging;

      const refreshing = store.refresh();
      expect(store.status()).toBe('ready');
      expect(store.busy()).toBe(true);
      expectList(http, { page: '2' }).flush(monthPayments({ page: 2, total: 120, received: 400 }));
      await refreshing;

      expect(store.data()?.received).toBe(400);
      expect(store.busy()).toBe(false);
    });

    it('volta uma pagina quando a atual ficou vazia', async () => {
      const { store, http } = setup();
      await shown(store, http, monthPayments({ total: 51 }));
      const paging = store.goToPage(2);
      expectList(http).flush(monthPayments({ page: 2, total: 51 }));
      await paging;

      const refreshing = store.refresh();
      expectList(http, { page: '2' }).flush(monthPayments({ page: 2, items: [], total: 50 }));
      await flushPromises();
      expectList(http, { page: '1' }).flush(monthPayments({ page: 1, total: 50 }));
      await refreshing;

      expect(store.query().page).toBe(1);
      expect(store.data()?.items).toHaveLength(1);
    });

    it('vira "nao fechado" quando o mes foi reaberto em outro lugar', async () => {
      const { store, http } = setup();
      await shown(store, http);

      const refreshing = store.refresh();
      expectList(http).flush(...fail(409, 'closing.not_closed'));
      await refreshing;

      expect(store.status()).toBe('not-closed');
    });
  });

  describe('register', () => {
    const PAYLOAD = { amount: 100, receivedOn: '2026-11-10', method: 'pix', note: null } as const;
    const POST = { method: 'POST', url: '/api/finance/charges/c1/payments' };

    it('registra, le o mes de novo e responde nulo', async () => {
      const { store, http } = setup();
      await shown(store, http);

      const registering = store.register('c1', PAYLOAD);
      expect(store.busy()).toBe(true);
      const post = http.expectOne(POST);
      expect(post.request.body).toEqual(PAYLOAD);
      post.flush({});
      await flushPromises();
      expectList(http).flush(monthPayments({ received: 100 }));

      expect(await registering).toBeNull();
      expect(store.busy()).toBe(false);
      expect(store.data()?.received).toBe(100);
    });

    it.each([
      [422, 'finance.payment.exceeds_balance'],
      [404, 'finance.charge.not_found'],
    ])('le o mes de novo quando a tela estava desatualizada (%s)', async (status, code) => {
      const { store, http } = setup();
      await shown(store, http);

      const registering = store.register('c1', PAYLOAD);
      http.expectOne(POST).flush(...fail(status, code));
      await flushPromises();
      expectList(http).flush(monthPayments({ received: 700 }));

      expect((await registering)?.code).toBe(code);
      expect(store.data()?.received).toBe(700);
    });

    it('devolve o problema sem ler o mes quando a API recusa um campo', async () => {
      const { store, http } = setup();
      await shown(store, http);
      const before = store.data();

      const registering = store.register('c1', PAYLOAD);
      http.expectOne(POST).flush(...fail(400, 'finance.payment.date_invalid'));

      expect((await registering)?.code).toBe('finance.payment.date_invalid');
      expect(store.data()).toBe(before);
      expect(store.busy()).toBe(false);
    });
  });
});
