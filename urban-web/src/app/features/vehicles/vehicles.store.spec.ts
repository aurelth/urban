import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { unit, unitPage } from '../../testing/units.fixtures';
import { vehicle, vehiclePage } from '../../testing/vehicles.fixtures';
import { VehiclePayload } from '../../core/vehicles/vehicles.models';
import { VehiclesStore } from './vehicles.store';

const PAYLOAD: VehiclePayload = {
  unitId: 'u-1',
  plate: 'ABC1D23',
  model: 'Fiat Argo',
  color: 'Prata',
};

function setup(): { store: InstanceType<typeof VehiclesStore>; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), VehiclesStore],
  });
  return { store: TestBed.inject(VehiclesStore), http: TestBed.inject(HttpTestingController) };
}

function listRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((request) => request.url === '/api/vehicles' && request.method === 'GET');
}

function unitsRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((request) => request.url === '/api/units');
}

function listParams(request: TestRequest): URLSearchParams {
  return new URLSearchParams(request.request.params.toString());
}

function fail(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

/** Responde à carga completa (veículos e unidades ativas), na ordem em que a tela a pede. */
async function loaded(
  store: InstanceType<typeof VehiclesStore>,
  http: HttpTestingController,
  items = [vehicle(), vehicle()],
  total = items.length,
  units = [unit(), unit()],
): Promise<void> {
  const loading = store.load();
  listRequest(http).flush(vehiclePage(items, total));
  unitsRequest(http).flush(unitPage(units));
  await loading;
}

describe('VehiclesStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('começa carregando, sem veículos, na primeira página e sem filtro', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.items()).toEqual([]);
    expect(store.units()).toEqual([]);
    expect(store.query()).toEqual({ unitId: null, search: '', page: 1, pageSize: 50 });
    expect(store.busy()).toBe(false);
    expect(store.hasFilters()).toBe(false);
  });

  describe('load', () => {
    it('busca os veículos e as unidades ativas juntos e passa a pronto', async () => {
      const { store, http } = setup();
      const items = [vehicle(), vehicle()];
      const units = [unit(), unit(), unit()];

      const loading = store.load();
      expect(store.status()).toBe('loading');
      listRequest(http).flush(vehiclePage(items, 7));
      unitsRequest(http).flush(unitPage(units));
      await loading;

      expect(store.status()).toBe('ready');
      expect(store.items()).toEqual(items);
      expect(store.total()).toBe(7);
      expect(store.units()).toEqual(units);
      expect(store.pageCount()).toBe(1);
    });

    it('vai a erro quando uma das buscas falha e tenta de novo', async () => {
      const { store, http } = setup();

      const failing = store.load();
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      unitsRequest(http).flush(unitPage([]));
      await failing;
      expect(store.status()).toBe('error');

      await loaded(store, http);
      expect(store.status()).toBe('ready');
    });
  });

  describe('filtros', () => {
    it('filtrar por unidade volta à página 1 e não busca as unidades de novo', async () => {
      const { store, http } = setup();
      await loaded(store, http, [vehicle()], 120);
      const going = store.goToPage(2);
      listRequest(http).flush(vehiclePage([vehicle()], 120, 2));
      await going;

      const filtering = store.filter({ unitId: 'u-9' });
      const request = listRequest(http);
      request.flush(vehiclePage([vehicle()], 1));
      await filtering;

      expect(listParams(request).get('unitId')).toBe('u-9');
      expect(listParams(request).get('page')).toBe('1');
      expect(store.query().unitId).toBe('u-9');
      expect(store.hasFilters()).toBe(true);
    });

    it('a busca por texto conta como filtro e limpar volta ao padrão', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const searching = store.filter({ search: 'argo' });
      listRequest(http).flush(vehiclePage([]));
      await searching;
      expect(store.hasFilters()).toBe(true);

      const clearing = store.filter({ unitId: null, search: '' });
      listRequest(http).flush(vehiclePage([vehicle()]));
      await clearing;
      expect(store.hasFilters()).toBe(false);
    });

    it('a falha da busca leva a erro e solta o "ocupado"', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const filtering = store.filter({ search: 'x' });
      expect(store.busy()).toBe(true);
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await filtering;

      expect(store.status()).toBe('error');
      expect(store.busy()).toBe(false);
    });
  });

  describe('goToPage', () => {
    it('busca a página pedida, mantendo os filtros', async () => {
      const { store, http } = setup();
      await loaded(store, http, [vehicle()], 120);
      const filtering = store.filter({ search: 'a' });
      listRequest(http).flush(vehiclePage([vehicle()], 120));
      await filtering;

      const going = store.goToPage(3);
      const request = listRequest(http);
      request.flush(vehiclePage([vehicle()], 120, 3));
      await going;

      expect(listParams(request).get('page')).toBe('3');
      expect(listParams(request).get('search')).toBe('a');
    });

    it.each([0, -1, 4, 1.5, Number.NaN])('ignora a página %s', async (page) => {
      const { store, http } = setup();
      await loaded(store, http, [vehicle()], 120);

      await store.goToPage(page);

      http.expectNone((request) => request.url === '/api/vehicles');
    });
  });

  describe('gravações', () => {
    it('cadastra, busca a lista de novo e devolve nulo', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const saving = store.create(PAYLOAD);
      const post = http.expectOne('/api/vehicles');
      expect(post.request.method).toBe('POST');
      expect(post.request.body).toEqual(PAYLOAD);
      post.flush(vehicle());
      await flushPromises();
      listRequest(http).flush(vehiclePage([vehicle()]));

      expect(await saving).toBeNull();
      expect(store.busy()).toBe(false);
    });

    it('altera levando a versão lida do veículo', async () => {
      const { store, http } = setup();
      const target = vehicle({ version: '41' });
      await loaded(store, http, [target]);

      const saving = store.update(target, PAYLOAD);
      const put = http.expectOne(`/api/vehicles/${target.id}`);
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual({ ...PAYLOAD, version: '41' });
      put.flush(target);
      await flushPromises();
      listRequest(http).flush(vehiclePage([target]));

      expect(await saving).toBeNull();
    });

    it('exclui e busca a lista de novo', async () => {
      const { store, http } = setup();
      const target = vehicle();
      await loaded(store, http, [target]);

      const removing = store.remove(target);
      const request = http.expectOne(`/api/vehicles/${target.id}`);
      expect(request.request.method).toBe('DELETE');
      request.flush(null, { status: 204, statusText: 'No Content' });
      await flushPromises();
      listRequest(http).flush(vehiclePage([]));

      expect(await removing).toBeNull();
      expect(store.items()).toEqual([]);
    });

    it('placa repetida é erro de campo: devolve o problema e não busca a lista', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const saving = store.create(PAYLOAD);
      http.expectOne('/api/vehicles').flush(...fail(409, 'vehicle.plate.already_registered'));
      const result = await saving;

      expect(result?.code).toBe('vehicle.plate.already_registered');
      http.expectNone((request) => request.url === '/api/vehicles' && request.method === 'GET');
      expect(store.busy()).toBe(false);
    });

    it.each([
      [409, 'concurrency.conflict'],
      [404, 'vehicle.not_found'],
    ])('tela desatualizada (%s %s): busca a lista de novo', async (status, code) => {
      const { store, http } = setup();
      const target = vehicle();
      await loaded(store, http, [target]);

      const saving = store.update(target, PAYLOAD);
      http.expectOne(`/api/vehicles/${target.id}`).flush(...fail(status, code));
      await flushPromises();
      listRequest(http).flush(vehiclePage([]));
      const result = await saving;

      expect(result?.code).toBe(code);
      http.expectNone('/api/units');
    });

    it.each(['unit.not_found', 'unit.deactivated'])(
      'recusa "%s": busca a lista e as unidades de novo',
      async (code) => {
        const { store, http } = setup();
        await loaded(store, http);

        const saving = store.create(PAYLOAD);
        http.expectOne('/api/vehicles').flush(...fail(code === 'unit.not_found' ? 404 : 409, code));
        await flushPromises();
        listRequest(http).flush(vehiclePage([vehicle()]));
        await flushPromises();
        unitsRequest(http).flush(unitPage([unit()]));
        const result = await saving;

        expect(result?.code).toBe(code);
        expect(store.units()).toHaveLength(1);
      },
    );

    it('se a página ficou vazia depois de excluir, volta uma página', async () => {
      const { store, http } = setup();
      await loaded(store, http, [vehicle()], 51);
      const going = store.goToPage(2);
      listRequest(http).flush(vehiclePage([vehicle()], 51, 2));
      await going;
      const target = store.items()[0];

      const removing = store.remove(target);
      http.expectOne(`/api/vehicles/${target.id}`).flush(null, { status: 204, statusText: 'x' });
      await flushPromises();
      listRequest(http).flush(vehiclePage([], 50, 2));
      await flushPromises();
      await flushPromises();
      const previous = listRequest(http);
      previous.flush(vehiclePage([vehicle()], 50, 1));
      await removing;

      expect(listParams(previous).get('page')).toBe('1');
      expect(store.query().page).toBe(1);
    });
  });
});
