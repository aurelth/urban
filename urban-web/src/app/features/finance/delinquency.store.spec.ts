import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { debtor, delinquency } from '../../testing/delinquency.fixtures';
import { DelinquencyStore } from './delinquency.store';

const LIST = '/api/finance/delinquency';
const RESEND = '/api/finance/charges/c1/invoice-deliveries';

type Store = InstanceType<typeof DelinquencyStore>;
interface Reply {
  flush: (body: unknown, init?: { status: number; statusText: string }) => void;
}

function setup(): { store: Store; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), DelinquencyStore],
  });
  return { store: TestBed.inject(DelinquencyStore), http: TestBed.inject(HttpTestingController) };
}

function expectList(http: HttpTestingController, params: Record<string, string> = {}): Reply {
  const request = http.expectOne((r) => r.url === LIST);
  expect(request.request.method).toBe('GET');
  for (const [name, value] of Object.entries(params)) {
    expect(request.request.params.get(name)).toBe(value);
  }
  return request;
}

async function loaded(
  store: Store,
  http: HttpTestingController,
  data = delinquency(),
): Promise<void> {
  const loading = store.load();
  expectList(http).flush(data);
  await loading;
}

function fail(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

describe('DelinquencyStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('começa carregando, sem dados e na primeira página', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.data()).toBeNull();
    expect(store.busy()).toBe(false);
    expect(store.query()).toEqual({ page: 1, pageSize: 50 });
    expect(store.pageCount()).toBe(1);
  });

  describe('load', () => {
    it('lê a primeira página e fica pronto', async () => {
      const { store, http } = setup();

      const loading = store.load();
      expect(store.status()).toBe('loading');
      expectList(http, { page: '1', pageSize: '50' }).flush(delinquency({ totalOverdue: 2500 }));
      await loading;

      expect(store.status()).toBe('ready');
      expect(store.data()?.totalOverdue).toBe(2500);
    });

    it('vira "erro" quando a leitura falha e pode tentar de novo', async () => {
      const { store, http } = setup();
      const first = store.load();
      expectList(http).flush(null, { status: 500, statusText: 'Erro' });
      await first;
      expect(store.status()).toBe('error');

      await loaded(store, http);

      expect(store.status()).toBe('ready');
    });

    it('volta para a primeira página', async () => {
      const { store, http } = setup();
      await loaded(store, http, delinquency({ units: 120 }));
      const paging = store.goToPage(2);
      expectList(http, { page: '2' }).flush(delinquency({ units: 120, page: 2 }));
      await paging;

      const reloading = store.load();
      expectList(http, { page: '1' }).flush(delinquency({ units: 120 }));
      await reloading;

      expect(store.query().page).toBe(1);
    });
  });

  describe('pageCount', () => {
    it.each([
      [0, 1],
      [1, 1],
      [50, 1],
      [51, 2],
      [120, 3],
    ])('com %i unidades devedoras tem %i página(s)', async (units, pages) => {
      const { store, http } = setup();

      await loaded(store, http, delinquency({ units }));

      expect(store.pageCount()).toBe(pages);
    });
  });

  describe('goToPage', () => {
    it('lê a página pedida e mantém a lista antiga enquanto lê', async () => {
      const { store, http } = setup();
      await loaded(store, http, delinquency({ units: 120, items: [debtor({ number: '101' })] }));

      const paging = store.goToPage(2);
      expect(store.busy()).toBe(true);
      expect(store.status()).toBe('ready');
      expect(store.data()?.items[0].number).toBe('101');
      expectList(http, { page: '2' }).flush(
        delinquency({ units: 120, page: 2, items: [debtor({ number: '301' })] }),
      );
      await paging;

      expect(store.busy()).toBe(false);
      expect(store.query().page).toBe(2);
      expect(store.data()?.items[0].number).toBe('301');
    });

    it.each([0, -1, 1.5, 4, Number.NaN])('ignora a página %s, que não existe', async (page) => {
      const { store, http } = setup();
      await loaded(store, http, delinquency({ units: 120 }));

      await store.goToPage(page);

      expect(store.busy()).toBe(false);
      expect(store.query().page).toBe(1);
    });

    it('vira "erro" quando a leitura falha', async () => {
      const { store, http } = setup();
      await loaded(store, http, delinquency({ units: 120 }));

      const paging = store.goToPage(2);
      expectList(http).flush(null, { status: 500, statusText: 'Erro' });
      await paging;

      expect(store.status()).toBe('error');
      expect(store.busy()).toBe(false);
    });
  });

  describe('refresh', () => {
    it('lê de novo a página atual', async () => {
      const { store, http } = setup();
      await loaded(store, http, delinquency({ units: 120 }));
      const paging = store.goToPage(2);
      expectList(http).flush(delinquency({ units: 120, page: 2 }));
      await paging;

      const refreshing = store.refresh();
      expect(store.busy()).toBe(true);
      expectList(http, { page: '2' }).flush(delinquency({ units: 120, page: 2, totalOverdue: 9 }));
      await refreshing;

      expect(store.busy()).toBe(false);
      expect(store.data()?.totalOverdue).toBe(9);
    });

    it('volta uma página quando a atual ficou vazia', async () => {
      const { store, http } = setup();
      await loaded(store, http, delinquency({ units: 51 }));
      const paging = store.goToPage(2);
      expectList(http).flush(delinquency({ units: 51, page: 2, items: [debtor()] }));
      await paging;

      const refreshing = store.refresh();
      expectList(http, { page: '2' }).flush(delinquency({ units: 50, page: 2, items: [] }));
      await flushPromises();
      expectList(http, { page: '1' }).flush(delinquency({ units: 50, page: 1 }));
      await refreshing;

      expect(store.query().page).toBe(1);
      expect(store.data()?.items).toHaveLength(1);
    });
  });

  describe('resend', () => {
    it('põe a fatura na fila e devolve nulo, sem ler a lista de novo', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const sending = store.resend('c1');
      expect(store.busy()).toBe(true);
      const request = http.expectOne(RESEND);
      expect(request.request.method).toBe('POST');
      request.flush(null, { status: 202, statusText: 'Accepted' });

      expect(await sending).toBeNull();
      expect(store.busy()).toBe(false);
    });

    it.each([
      [404, 'finance.charge.not_found'],
      [409, 'finance.invoice_delivery.already_queued'],
      [422, 'finance.invoice_delivery.no_recipient'],
    ])(
      'devolve o problema %i e lê a lista de novo, porque a tela estava desatualizada',
      async (status, code) => {
        const { store, http } = setup();
        await loaded(store, http);

        const sending = store.resend('c1');
        http.expectOne(RESEND).flush(...fail(status, code));
        await flushPromises();
        expectList(http).flush(delinquency({ totalOverdue: 7 }));
        const result = await sending;

        expect(result?.code).toBe(code);
        expect(result?.status).toBe(status);
        expect(store.data()?.totalOverdue).toBe(7);
        expect(store.busy()).toBe(false);
      },
    );

    it.each([
      [429, 'rate_limit.exceeded'],
      [500, 'internal'],
    ])('devolve o problema %i sem ler a lista de novo', async (status, code) => {
      const { store, http } = setup();
      await loaded(store, http);

      const sending = store.resend('c1');
      http.expectOne(RESEND).flush(...fail(status, code));
      const result = await sending;

      expect(result?.status).toBe(status);
      expect(store.busy()).toBe(false);
    });
  });
});
