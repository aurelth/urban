import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { UnitPayload } from '../../core/units/units.models';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { unit, unitPage, unitsSummary } from '../../testing/units.fixtures';
import { UnitsStore } from './units.store';

const PAYLOAD: UnitPayload = {
  block: 'A',
  number: '101',
  type: 'apartment',
  idealFraction: 0.0125,
  area: 52.5,
};

function setup(): { store: InstanceType<typeof UnitsStore>; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), UnitsStore],
  });
  return { store: TestBed.inject(UnitsStore), http: TestBed.inject(HttpTestingController) };
}

function listRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((request) => request.url === '/api/units' && request.method === 'GET');
}

function listParams(request: TestRequest): URLSearchParams {
  return new URLSearchParams(request.request.params.toString());
}

function fail(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

/** Responde a carga completa (lista e resumo), na ordem em que a tela a pede. */
async function loaded(
  store: InstanceType<typeof UnitsStore>,
  http: HttpTestingController,
  items = [unit(), unit()],
  total = items.length,
): Promise<void> {
  const loading = store.load();
  listRequest(http).flush(unitPage(items, total));
  http.expectOne('/api/units/summary').flush(unitsSummary());
  await loading;
}

describe('UnitsStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('comeca carregando, sem unidades, nas ativas da primeira pagina', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.items()).toEqual([]);
    expect(store.summary()).toBeNull();
    expect(store.query()).toEqual({
      status: 'active',
      type: null,
      block: '',
      search: '',
      page: 1,
      pageSize: 50,
    });
    expect(store.busy()).toBe(false);
  });

  describe('load', () => {
    it('busca a lista e o resumo juntos e passa a pronto', async () => {
      const { store, http } = setup();
      const items = [unit(), unit()];

      const loading = store.load();
      expect(store.status()).toBe('loading');
      listRequest(http).flush(unitPage(items, 2));
      http.expectOne('/api/units/summary').flush(unitsSummary({ active: 2 }));
      await loading;

      expect(store.status()).toBe('ready');
      expect(store.items()).toEqual(items);
      expect(store.total()).toBe(2);
      expect(store.summary()?.active).toBe(2);
    });

    it('vira "erro" quando a lista falha', async () => {
      const { store, http } = setup();

      const loading = store.load();
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      http.expectOne('/api/units/summary').flush(unitsSummary());
      await loading;

      expect(store.status()).toBe('error');
    });

    it('vira "erro" quando o resumo falha', async () => {
      const { store, http } = setup();

      const loading = store.load();
      listRequest(http).flush(unitPage([]));
      http.expectOne('/api/units/summary').flush(null, { status: 503, statusText: 'Erro' });
      await loading;

      expect(store.status()).toBe('error');
    });

    it('depois de um erro, buscar de novo volta a carregar e pode dar certo', async () => {
      const { store, http } = setup();
      const first = store.load();
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      http.expectOne('/api/units/summary').flush(null, { status: 500, statusText: 'Erro' });
      await first;

      await loaded(store, http);

      expect(store.status()).toBe('ready');
    });
  });

  describe('pageCount e hasFilters', () => {
    it.each([
      [0, 1],
      [50, 1],
      [51, 2],
      [120, 3],
    ])('com %s unidades ha %s pagina(s)', async (total, pages) => {
      const { store, http } = setup();

      await loaded(store, http, [unit()], total);

      expect(store.pageCount()).toBe(pages);
    });

    it('so considera filtro o que foge do padrao (ativas, sem tipo, bloco ou busca)', async () => {
      const { store, http } = setup();
      await loaded(store, http);
      expect(store.hasFilters()).toBe(false);

      const types = store.filter({ type: 'house' });
      listRequest(http).flush(unitPage([]));
      await types;
      expect(store.hasFilters()).toBe(true);

      const clean = store.filter({ type: null });
      listRequest(http).flush(unitPage([]));
      await clean;
      expect(store.hasFilters()).toBe(false);
    });
  });

  describe('filter', () => {
    it('busca de novo so a lista, a partir da pagina 1, e mantem o resumo', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const filtering = store.filter({ status: 'deactivated', block: 'B' });
      expect(store.busy()).toBe(true);
      const request = listRequest(http);
      request.flush(unitPage([unit({ isActive: false })], 1));
      await filtering;

      const params = listParams(request);
      expect(params.get('status')).toBe('deactivated');
      expect(params.get('block')).toBe('B');
      expect(params.get('page')).toBe('1');
      expect(store.query()).toMatchObject({ status: 'deactivated', block: 'B', page: 1 });
      expect(store.items()).toHaveLength(1);
      expect(store.summary()).not.toBeNull();
      expect(store.status()).toBe('ready');
      expect(store.busy()).toBe(false);
    });

    it('volta para a pagina 1 quando o filtro muda estando em outra pagina', async () => {
      const { store, http } = setup();
      await loaded(store, http, [unit()], 150);
      const paging = store.goToPage(3);
      listRequest(http).flush(unitPage([unit()], 150, 3));
      await paging;

      const filtering = store.filter({ search: '10' });
      const request = listRequest(http);
      request.flush(unitPage([], 0));
      await filtering;

      expect(listParams(request).get('page')).toBe('1');
    });

    it('vira "erro" quando a busca filtrada falha', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const filtering = store.filter({ type: 'house' });
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await filtering;

      expect(store.status()).toBe('error');
      expect(store.busy()).toBe(false);
    });
  });

  describe('goToPage', () => {
    it('busca a pagina pedida mantendo os filtros', async () => {
      const { store, http } = setup();
      await loaded(store, http, [unit()], 120);
      const filtering = store.filter({ type: 'apartment' });
      listRequest(http).flush(unitPage([unit()], 120));
      await filtering;

      const paging = store.goToPage(2);
      const request = listRequest(http);
      request.flush(unitPage([unit()], 120, 2));
      await paging;

      expect(listParams(request).get('page')).toBe('2');
      expect(listParams(request).get('type')).toBe('apartment');
      expect(store.query().page).toBe(2);
    });

    it.each([0, -1, 4, 1.5])('ignora a pagina %s, que nao existe', async (page) => {
      const { store, http } = setup();
      await loaded(store, http, [unit()], 150);

      await store.goToPage(page);

      http.expectNone((request) => request.url === '/api/units');
      expect(store.query().page).toBe(1);
    });
  });

  describe('create', () => {
    it('cadastra e atualiza a lista e o resumo', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const creating = store.create(PAYLOAD);
      expect(store.busy()).toBe(true);
      const post = http.expectOne(
        (request) => request.method === 'POST' && request.url === '/api/units',
      );
      expect(post.request.body).toEqual(PAYLOAD);
      post.flush(unit());
      await flushPromises();
      listRequest(http).flush(unitPage([unit(), unit(), unit()], 3));
      http.expectOne('/api/units/summary').flush(unitsSummary({ active: 3 }));

      expect(await creating).toBeNull();
      expect(store.items()).toHaveLength(3);
      expect(store.summary()?.active).toBe(3);
      expect(store.busy()).toBe(false);
    });

    it('devolve o problema e deixa a lista como estava quando a API recusa', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const creating = store.create(PAYLOAD);
      const [body, options] = fail(409, 'unit.identifier.already_exists');
      http.expectOne('/api/units').flush(body, options);

      const result = await creating;

      expect(result).toMatchObject({ status: 409, code: 'unit.identifier.already_exists' });
      expect(store.items()).toHaveLength(2);
      expect(store.status()).toBe('ready');
      expect(store.busy()).toBe(false);
    });
  });

  describe('update', () => {
    it('manda a versao da unidade lida e atualiza a lista e o resumo', async () => {
      const { store, http } = setup();
      const target = unit({ version: '41' });
      await loaded(store, http, [target]);

      const updating = store.update(target, PAYLOAD);
      const put = http.expectOne(`/api/units/${target.id}`);
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual({ ...PAYLOAD, version: '41' });
      put.flush({ ...target, version: '42' });
      await flushPromises();
      listRequest(http).flush(unitPage([{ ...target, version: '42' }]));
      http.expectOne('/api/units/summary').flush(unitsSummary());

      expect(await updating).toBeNull();
      expect(store.items()[0].version).toBe('42');
    });

    it.each([
      [409, 'concurrency.conflict'],
      [404, 'unit.not_found'],
      [409, 'unit.deactivated'],
    ])('com %s %s, devolve o problema e recarrega a lista e o resumo', async (status, code) => {
      const { store, http } = setup();
      const target = unit();
      await loaded(store, http, [target]);

      const updating = store.update(target, PAYLOAD);
      const [body, options] = fail(status, code);
      http.expectOne(`/api/units/${target.id}`).flush(body, options);
      await flushPromises();
      listRequest(http).flush(unitPage([]));
      http.expectOne('/api/units/summary').flush(unitsSummary());

      expect(await updating).toMatchObject({ status, code });
      expect(store.items()).toEqual([]);
    });

    it('com recusa de campo (400), devolve o problema sem recarregar', async () => {
      const { store, http } = setup();
      const target = unit();
      await loaded(store, http, [target]);

      const updating = store.update(target, PAYLOAD);
      const [body, options] = fail(400, 'unit.number.invalid');
      http.expectOne(`/api/units/${target.id}`).flush(body, options);

      expect(await updating).toMatchObject({ status: 400, code: 'unit.number.invalid' });
      http.expectNone((request) => request.url === '/api/units');
    });
  });

  describe.each(['deactivate', 'reactivate'] as const)('%s', (action) => {
    it('chama a acao com a versao e atualiza a lista e o resumo', async () => {
      const { store, http } = setup();
      const target = unit({ version: '7' });
      await loaded(store, http, [target]);

      const running = store[action](target);
      const post = http.expectOne(`/api/units/${target.id}/${action}`);
      expect(post.request.body).toEqual({ version: '7' });
      post.flush({ ...target, version: '8' });
      await flushPromises();
      listRequest(http).flush(unitPage([]));
      http.expectOne('/api/units/summary').flush(unitsSummary({ active: 0 }));

      expect(await running).toBeNull();
      expect(store.items()).toEqual([]);
      expect(store.summary()?.active).toBe(0);
    });

    it('com 409 de versao, devolve o problema e recarrega', async () => {
      const { store, http } = setup();
      const target = unit();
      await loaded(store, http, [target]);

      const running = store[action](target);
      const [body, options] = fail(409, 'concurrency.conflict');
      http.expectOne(`/api/units/${target.id}/${action}`).flush(body, options);
      await flushPromises();
      listRequest(http).flush(unitPage([target]));
      http.expectOne('/api/units/summary').flush(unitsSummary());

      expect(await running).toMatchObject({ status: 409, code: 'concurrency.conflict' });
    });

    it('com a API fora do ar, devolve o problema sem recarregar', async () => {
      const { store, http } = setup();
      const target = unit();
      await loaded(store, http, [target]);

      const running = store[action](target);
      http.expectOne(`/api/units/${target.id}/${action}`).error(new ProgressEvent('error'));

      expect(await running).toMatchObject({ status: 0 });
      http.expectNone((request) => request.url === '/api/units');
    });
  });

  it('se a ultima unidade da pagina some, volta para a pagina anterior', async () => {
    const { store, http } = setup();
    const only = unit();
    await loaded(store, http, [unit()], 51);
    const paging = store.goToPage(2);
    listRequest(http).flush(unitPage([only], 51, 2));
    await paging;

    const running = store.deactivate(only);
    http.expectOne(`/api/units/${only.id}/deactivate`).flush({ ...only, isActive: false });
    await flushPromises();
    listRequest(http).flush(unitPage([], 50, 2));
    http.expectOne('/api/units/summary').flush(unitsSummary());
    await flushPromises();
    const previous = listRequest(http);
    previous.flush(unitPage([unit()], 50, 1));
    await running;

    expect(listParams(previous).get('page')).toBe('1');
    expect(store.query().page).toBe(1);
    expect(store.items()).toHaveLength(1);
  });

  describe('changeMethod', () => {
    it('manda a versao do resumo e passa a mostrar o metodo e a versao novos', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const changing = store.changeMethod('area');
      expect(store.busy()).toBe(true);
      const put = http.expectOne('/api/units/apportionment-method');
      expect(put.request.body).toEqual({ version: '12', method: 'area' });
      put.flush({ method: 'area', version: '13' });

      expect(await changing).toBeNull();
      expect(store.summary()).toMatchObject({
        apportionmentMethod: 'area',
        apportionmentVersion: '13',
      });
      expect(store.busy()).toBe(false);
    });

    it('com 409 de versao, devolve o problema e recarrega o resumo', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const changing = store.changeMethod('equalShare');
      const [body, options] = fail(409, 'concurrency.conflict');
      http.expectOne('/api/units/apportionment-method').flush(body, options);
      await flushPromises();
      http
        .expectOne('/api/units/summary')
        .flush(unitsSummary({ apportionmentMethod: 'area', apportionmentVersion: '14' }));

      expect(await changing).toMatchObject({ status: 409, code: 'concurrency.conflict' });
      expect(store.summary()).toMatchObject({
        apportionmentMethod: 'area',
        apportionmentVersion: '14',
      });
    });

    it('sem resumo carregado, nao faz nada', async () => {
      const { store, http } = setup();

      expect(await store.changeMethod('area')).toBeNull();
      http.expectNone('/api/units/apportionment-method');
    });
  });
});
