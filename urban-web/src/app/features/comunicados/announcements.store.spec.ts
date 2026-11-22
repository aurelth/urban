import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AnnouncementPayload } from '../../core/announcements/announcements.models';
import { announcement, announcementPage } from '../../testing/announcements.fixtures';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { unit, unitPage } from '../../testing/units.fixtures';
import { AnnouncementsStore } from './announcements.store';

const PAYLOAD: AnnouncementPayload = {
  title: 'Água cortada na terça',
  body: 'A rua será interditada.',
  category: 'maintenance',
  scope: 'condominium',
  block: null,
  unitId: null,
};

type Store = InstanceType<typeof AnnouncementsStore>;

function setup(): { store: Store; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), AnnouncementsStore],
  });
  return { store: TestBed.inject(AnnouncementsStore), http: TestBed.inject(HttpTestingController) };
}

function listRequest(http: HttpTestingController): TestRequest {
  return http.expectOne(
    (request) => request.url === '/api/announcements' && request.method === 'GET',
  );
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

/** Responde à carga completa (comunicados e unidades ativas), na ordem em que a tela a pede. */
async function loaded(
  store: Store,
  http: HttpTestingController,
  items = [announcement(), announcement()],
  total = items.length,
  units = [unit(), unit()],
): Promise<void> {
  const loading = store.load();
  listRequest(http).flush(announcementPage(items, total));
  unitsRequest(http).flush(unitPage(units));
  await loading;
}

describe('AnnouncementsStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('começa carregando, sem comunicados, na primeira página', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.items()).toEqual([]);
    expect(store.units()).toEqual([]);
    expect(store.page()).toBe(1);
    expect(store.busy()).toBe(false);
    expect(store.pageCount()).toBe(1);
  });

  describe('load', () => {
    it('busca a primeira página de 20 e as unidades ativas, e passa a pronto', async () => {
      const { store, http } = setup();
      const items = [announcement(), announcement()];
      const units = [unit(), unit(), unit()];

      const loading = store.load();
      expect(store.status()).toBe('loading');
      const request = listRequest(http);
      request.flush(announcementPage(items, 45));
      unitsRequest(http).flush(unitPage(units));
      await loading;

      expect(listParams(request).toString()).toBe('page=1&pageSize=20');
      expect(store.status()).toBe('ready');
      expect(store.items()).toEqual(items);
      expect(store.total()).toBe(45);
      expect(store.units()).toEqual(units);
      expect(store.pageCount()).toBe(3);
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

  describe('blocks', () => {
    it('junta os blocos das unidades ativas, sem repetir, em ordem natural e sem as unidades sem bloco', async () => {
      const { store, http } = setup();
      const units = [
        unit({ block: 'B' }),
        unit({ block: '10' }),
        unit({ block: 'A' }),
        unit({ block: 'B' }),
        unit({ block: '2' }),
        unit({ block: null }),
      ];

      await loaded(store, http, [announcement()], 1, units);

      expect(store.blocks()).toEqual(['2', '10', 'A', 'B']);
    });
  });

  describe('goToPage', () => {
    it('busca a página pedida', async () => {
      const { store, http } = setup();
      await loaded(store, http, [announcement()], 60);

      const going = store.goToPage(3);
      expect(store.busy()).toBe(true);
      const request = listRequest(http);
      request.flush(announcementPage([announcement()], 60, 3));
      await going;

      expect(listParams(request).get('page')).toBe('3');
      expect(store.page()).toBe(3);
      expect(store.busy()).toBe(false);
    });

    it.each([0, -1, 4, 1.5, Number.NaN])('ignora a página %s', async (page) => {
      const { store, http } = setup();
      await loaded(store, http, [announcement()], 60);

      await store.goToPage(page);

      http.expectNone((request) => request.url === '/api/announcements');
    });

    it('a falha da busca leva a erro e solta o "ocupado"', async () => {
      const { store, http } = setup();
      await loaded(store, http, [announcement()], 60);

      const going = store.goToPage(2);
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await going;

      expect(store.status()).toBe('error');
      expect(store.busy()).toBe(false);
    });
  });

  describe('gravações', () => {
    it('publica, volta à primeira página e devolve nulo', async () => {
      const { store, http } = setup();
      await loaded(store, http, [announcement()], 60);
      const going = store.goToPage(2);
      listRequest(http).flush(announcementPage([announcement()], 60, 2));
      await going;

      const saving = store.create(PAYLOAD);
      const post = http.expectOne('/api/announcements');
      expect(post.request.method).toBe('POST');
      expect(post.request.body).toEqual(PAYLOAD);
      post.flush(announcement());
      await flushPromises();
      const refresh = listRequest(http);
      refresh.flush(announcementPage([announcement()], 61, 1));

      expect(await saving).toBeNull();
      expect(listParams(refresh).get('page')).toBe('1');
      expect(store.page()).toBe(1);
      expect(store.total()).toBe(61);
      expect(store.busy()).toBe(false);
    });

    it('altera levando a versão lida e fica na mesma página', async () => {
      const { store, http } = setup();
      const target = announcement({ version: '41' });
      await loaded(store, http, [target]);

      const saving = store.update(target, PAYLOAD);
      const put = http.expectOne(`/api/announcements/${target.id}`);
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual({ ...PAYLOAD, version: '41' });
      put.flush(target);
      await flushPromises();
      listRequest(http).flush(announcementPage([target]));

      expect(await saving).toBeNull();
    });

    it('exclui levando a versão e busca a lista de novo', async () => {
      const { store, http } = setup();
      const target = announcement({ version: '9' });
      await loaded(store, http, [target]);

      const removing = store.remove(target);
      const request = http.expectOne(
        (candidate) =>
          candidate.url === `/api/announcements/${target.id}` && candidate.method === 'DELETE',
      );
      expect(request.request.params.get('version')).toBe('9');
      request.flush(null, { status: 204, statusText: 'No Content' });
      await flushPromises();
      listRequest(http).flush(announcementPage([]));

      expect(await removing).toBeNull();
      expect(store.items()).toEqual([]);
    });

    it('recusa de campo devolve o problema e não busca a lista', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const saving = store.create(PAYLOAD);
      http.expectOne('/api/announcements').flush(...fail(422, 'announcement.block.not_found'));
      const result = await saving;

      expect(result?.code).toBe('announcement.block.not_found');
      http.expectNone(
        (request) => request.url === '/api/announcements' && request.method === 'GET',
      );
      expect(store.busy()).toBe(false);
    });

    it.each([
      [409, 'concurrency.conflict'],
      [404, 'announcement.not_found'],
    ])('tela desatualizada (%s %s): busca a lista de novo', async (status, code) => {
      const { store, http } = setup();
      const target = announcement();
      await loaded(store, http, [target]);

      const saving = store.update(target, PAYLOAD);
      http.expectOne(`/api/announcements/${target.id}`).flush(...fail(status, code));
      await flushPromises();
      listRequest(http).flush(announcementPage([]));
      const result = await saving;

      expect(result?.code).toBe(code);
      expect(store.items()).toEqual([]);
      http.expectNone('/api/units');
    });

    it('se a página ficou vazia depois de excluir, volta uma página', async () => {
      const { store, http } = setup();
      await loaded(store, http, [announcement()], 21);
      const going = store.goToPage(2);
      listRequest(http).flush(announcementPage([announcement()], 21, 2));
      await going;
      const target = store.items()[0];

      const removing = store.remove(target);
      http
        .expectOne(
          (candidate) =>
            candidate.url === `/api/announcements/${target.id}` && candidate.method === 'DELETE',
        )
        .flush(null, { status: 204, statusText: 'No Content' });
      await flushPromises();
      listRequest(http).flush(announcementPage([], 20, 2));
      await flushPromises();
      await flushPromises();
      const previous = listRequest(http);
      previous.flush(announcementPage([announcement()], 20, 1));
      await removing;

      expect(listParams(previous).get('page')).toBe('1');
      expect(store.page()).toBe(1);
    });
  });
});
