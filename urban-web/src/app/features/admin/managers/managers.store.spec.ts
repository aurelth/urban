import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { flushPromises } from '../../../testing/async';
import {
  MANAGERS_URL,
  SUSPENDED_MANAGER,
  manager,
  managersPage,
} from '../../../testing/admin.fixtures';
import { problem } from '../../../testing/auth.fixtures';
import { ManagersStore } from './managers.store';

const NO_CONTENT = { status: 204, statusText: 'No Content' };

function setup(): { store: InstanceType<typeof ManagersStore>; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), ManagersStore],
  });
  return { store: TestBed.inject(ManagersStore), http: TestBed.inject(HttpTestingController) };
}

function listRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((request) => request.url === MANAGERS_URL && request.method === 'GET');
}

function listParams(request: TestRequest): URLSearchParams {
  return new URLSearchParams(request.request.params.toString());
}

function fail(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

async function loaded(
  store: InstanceType<typeof ManagersStore>,
  http: HttpTestingController,
  items = [manager(), SUSPENDED_MANAGER],
  total = items.length,
): Promise<void> {
  const loading = store.load();
  listRequest(http).flush(managersPage(items, total));
  await loading;
}

describe('ManagersStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('comeca carregando, sem sindicos, em todas as situacoes e na primeira pagina', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.items()).toEqual([]);
    expect(store.total()).toBe(0);
    expect(store.query()).toEqual({ status: '', search: '', page: 1, pageSize: 20 });
    expect(store.busy()).toBe(false);
  });

  describe('load', () => {
    it('busca a lista e passa a pronto', async () => {
      const { store, http } = setup();
      const items = [manager(), SUSPENDED_MANAGER];

      const loading = store.load();
      expect(store.status()).toBe('loading');
      listRequest(http).flush(managersPage(items, 2));
      await loading;

      expect(store.status()).toBe('ready');
      expect(store.items()).toEqual(items);
      expect(store.total()).toBe(2);
    });

    it('vira "erro" quando a lista falha e pode tentar de novo', async () => {
      const { store, http } = setup();
      const first = store.load();
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await first;
      expect(store.status()).toBe('error');

      await loaded(store, http);

      expect(store.status()).toBe('ready');
    });
  });

  describe('pageCount e hasFilters', () => {
    it.each([
      [0, 1],
      [20, 1],
      [21, 2],
      [45, 3],
    ])('com %s sindicos ha %s pagina(s)', async (total, pages) => {
      const { store, http } = setup();

      await loaded(store, http, [manager()], total);

      expect(store.pageCount()).toBe(pages);
    });

    it('so considera filtro a situacao ou a busca preenchidas', async () => {
      const { store, http } = setup();
      await loaded(store, http);
      expect(store.hasFilters()).toBe(false);

      const suspended = store.filter({ status: 'suspended' });
      listRequest(http).flush(managersPage([]));
      await suspended;
      expect(store.hasFilters()).toBe(true);

      const spaces = store.filter({ status: '', search: '   ' });
      listRequest(http).flush(managersPage([]));
      await spaces;
      expect(store.hasFilters()).toBe(false);
    });
  });

  describe('filter', () => {
    it('busca de novo a partir da pagina 1', async () => {
      const { store, http } = setup();
      await loaded(store, http, [manager()], 60);
      const paging = store.goToPage(3);
      listRequest(http).flush(managersPage([manager()], 60, 3));
      await paging;

      const filtering = store.filter({ status: 'active', search: 'ana' });
      expect(store.busy()).toBe(true);
      const request = listRequest(http);
      request.flush(managersPage([manager()], 1));
      await filtering;

      const params = listParams(request);
      expect(params.get('status')).toBe('active');
      expect(params.get('search')).toBe('ana');
      expect(params.get('page')).toBe('1');
      expect(store.query()).toMatchObject({ status: 'active', search: 'ana', page: 1 });
      expect(store.busy()).toBe(false);
    });

    it('vira "erro" quando a busca filtrada falha', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const filtering = store.filter({ status: 'active' });
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await filtering;

      expect(store.status()).toBe('error');
      expect(store.busy()).toBe(false);
    });
  });

  describe('goToPage', () => {
    it('busca a pagina pedida mantendo os filtros', async () => {
      const { store, http } = setup();
      await loaded(store, http, [manager()], 45);
      const filtering = store.filter({ status: 'active' });
      listRequest(http).flush(managersPage([manager()], 45));
      await filtering;

      const paging = store.goToPage(2);
      const request = listRequest(http);
      request.flush(managersPage([SUSPENDED_MANAGER], 45, 2));
      await paging;

      expect(listParams(request).get('page')).toBe('2');
      expect(listParams(request).get('status')).toBe('active');
      expect(store.items()).toEqual([SUSPENDED_MANAGER]);
    });

    it.each([0, 4, 1.5, Number.NaN])('ignora a pagina %s, que nao existe', async (page) => {
      const { store, http } = setup();
      await loaded(store, http, [manager()], 45);

      await store.goToPage(page);

      http.expectNone(MANAGERS_URL);
      expect(store.query().page).toBe(1);
    });
  });

  describe('suspend', () => {
    it('manda o motivo, busca a lista de novo e devolve nulo', async () => {
      const { store, http } = setup();
      const target = manager();
      await loaded(store, http, [target, SUSPENDED_MANAGER]);

      const suspending = store.suspend(target, 'Uso indevido');
      expect(store.busy()).toBe(true);
      const request = http.expectOne(`${MANAGERS_URL}/${target.id}/suspend`);
      expect(request.request.body).toEqual({ reason: 'Uso indevido' });
      request.flush(null, NO_CONTENT);
      await flushPromises();
      listRequest(http).flush(
        managersPage([manager({ status: 'Suspended', statusReason: 'Uso indevido' })]),
      );

      await expect(suspending).resolves.toBeNull();
      expect(store.items()[0].status).toBe('Suspended');
      expect(store.busy()).toBe(false);
    });

    it.each([
      [404, 'identity.manager.not_found'],
      [409, 'identity.user.invalid_transition'],
    ])(
      'recusa %s: devolve o problema e busca a lista de novo, que estava desatualizada',
      async (status, code) => {
        const { store, http } = setup();
        const target = manager();
        await loaded(store, http);

        const suspending = store.suspend(target, 'Motivo');
        http.expectOne(`${MANAGERS_URL}/${target.id}/suspend`).flush(...fail(status, code));
        await flushPromises();
        listRequest(http).flush(managersPage([SUSPENDED_MANAGER]));
        const result = await suspending;

        expect(result?.code).toBe(code);
        expect(store.items()).toEqual([SUSPENDED_MANAGER]);
        expect(store.busy()).toBe(false);
      },
    );

    it.each([
      [400, 'identity.user.reason_required'],
      [403, 'identity.administration.forbidden'],
      [500, 'server.error'],
    ])('recusa %s: devolve o problema e nao busca a lista', async (status, code) => {
      const { store, http } = setup();
      const target = manager();
      await loaded(store, http);

      const suspending = store.suspend(target, 'Motivo');
      http.expectOne(`${MANAGERS_URL}/${target.id}/suspend`).flush(...fail(status, code));
      const result = await suspending;

      expect(result?.status).toBe(status);
      http.expectNone(MANAGERS_URL);
      expect(store.busy()).toBe(false);
    });
  });

  describe('reactivate', () => {
    it('reativa, busca a lista de novo e devolve nulo', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const reactivating = store.reactivate(SUSPENDED_MANAGER);
      const request = http.expectOne(`${MANAGERS_URL}/${SUSPENDED_MANAGER.id}/reactivate`);
      expect(request.request.method).toBe('POST');
      request.flush(null, NO_CONTENT);
      await flushPromises();
      listRequest(http).flush(managersPage([manager(), manager({ status: 'Active' })]));

      await expect(reactivating).resolves.toBeNull();
      expect(store.busy()).toBe(false);
    });

    it('recusa 409: devolve o problema e busca a lista de novo', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const reactivating = store.reactivate(SUSPENDED_MANAGER);
      http
        .expectOne(`${MANAGERS_URL}/${SUSPENDED_MANAGER.id}/reactivate`)
        .flush(...fail(409, 'identity.user.invalid_transition'));
      await flushPromises();
      listRequest(http).flush(managersPage([manager()]));
      const result = await reactivating;

      expect(result?.code).toBe('identity.user.invalid_transition');
      expect(store.items()).toEqual([manager()]);
    });
  });

  describe('depois de uma acao', () => {
    it('volta uma pagina quando a pagina atual ficou vazia', async () => {
      const { store, http } = setup();
      await loaded(store, http, [manager()], 21);
      const paging = store.goToPage(2);
      listRequest(http).flush(managersPage([SUSPENDED_MANAGER], 21, 2));
      await paging;

      const reactivating = store.reactivate(SUSPENDED_MANAGER);
      http.expectOne(`${MANAGERS_URL}/${SUSPENDED_MANAGER.id}/reactivate`).flush(null, NO_CONTENT);
      await flushPromises();
      listRequest(http).flush(managersPage([], 20, 2));
      await flushPromises();
      const back = listRequest(http);
      back.flush(managersPage([manager()], 20, 1));
      await reactivating;

      expect(listParams(back).get('page')).toBe('1');
      expect(store.query().page).toBe(1);
      expect(store.items()).toEqual([manager()]);
    });

    it('vira "erro" quando a busca que segue a acao falha', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const reactivating = store.reactivate(SUSPENDED_MANAGER);
      http.expectOne(`${MANAGERS_URL}/${SUSPENDED_MANAGER.id}/reactivate`).flush(null, NO_CONTENT);
      await flushPromises();
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await reactivating;

      expect(store.status()).toBe('error');
      expect(store.busy()).toBe(false);
    });
  });
});
