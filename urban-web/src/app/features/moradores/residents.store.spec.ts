import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { flushPromises } from '../../testing/async';
import { resident, residentPage } from '../../testing/residents.fixtures';
import { ResidentsStore } from './residents.store';

function setup(): { store: InstanceType<typeof ResidentsStore>; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), ResidentsStore],
  });
  return { store: TestBed.inject(ResidentsStore), http: TestBed.inject(HttpTestingController) };
}

function listRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((request) => request.url === '/api/residents' && request.method === 'GET');
}

function listParams(request: TestRequest): URLSearchParams {
  return new URLSearchParams(request.request.params.toString());
}

async function loaded(
  store: InstanceType<typeof ResidentsStore>,
  http: HttpTestingController,
  items = [resident(), resident()],
  total = items.length,
): Promise<void> {
  const loading = store.load();
  listRequest(http).flush(residentPage(items, total));
  await loading;
}

describe('ResidentsStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  describe('load', () => {
    it('começa carregando, com os moradores atuais, e vira "ready" com a página', async () => {
      const { store, http } = setup();
      const items = [resident(), resident()];

      const loading = store.load();
      expect(store.status()).toBe('loading');
      const request = listRequest(http);
      request.flush(residentPage(items, 2));
      await loading;

      expect(listParams(request).get('status')).toBe('active');
      expect(store.status()).toBe('ready');
      expect(store.items()).toEqual(items);
      expect(store.total()).toBe(2);
    });

    it('vira "error" quando a busca falha e dá para tentar de novo', async () => {
      const { store, http } = setup();
      const first = store.load();
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await first;
      expect(store.status()).toBe('error');

      await loaded(store, http);

      expect(store.status()).toBe('ready');
    });
  });

  describe('filter', () => {
    it('muda a situação e volta para a primeira página', async () => {
      const { store, http } = setup();
      await loaded(store, http, [resident()], 120);
      const second = store.goToPage(2);
      listRequest(http).flush(residentPage([resident()], 120, 2));
      await second;

      const filtering = store.filter({ status: 'former' });
      const request = listRequest(http);
      request.flush(residentPage([resident()], 1));
      await filtering;

      expect(listParams(request).get('status')).toBe('former');
      expect(listParams(request).get('page')).toBe('1');
      expect(store.query().status).toBe('former');
    });

    it('muda a busca e mantém a situação escolhida', async () => {
      const { store, http } = setup();
      await loaded(store, http);
      const changing = store.filter({ status: 'all' });
      listRequest(http).flush(residentPage([]));
      await changing;

      const searching = store.filter({ search: 'maria' });
      const request = listRequest(http);
      request.flush(residentPage([]));
      await searching;

      expect(listParams(request).get('search')).toBe('maria');
      expect(listParams(request).get('status')).toBe('all');
    });

    it('trava a tela enquanto busca e destrava depois', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const filtering = store.filter({ status: 'all' });
      expect(store.busy()).toBe(true);
      listRequest(http).flush(residentPage([]));
      await filtering;

      expect(store.busy()).toBe(false);
    });

    it('vira "error" quando a busca filtrada falha', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const filtering = store.filter({ search: 'x' });
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await filtering;

      expect(store.status()).toBe('error');
      expect(store.busy()).toBe(false);
    });
  });

  describe('goToPage', () => {
    it('busca a página pedida', async () => {
      const { store, http } = setup();
      await loaded(store, http, [resident()], 120);

      const going = store.goToPage(3);
      const request = listRequest(http);
      request.flush(residentPage([resident()], 120, 3));
      await going;

      expect(listParams(request).get('page')).toBe('3');
      expect(store.query().page).toBe(3);
    });

    it.each([0, -1, 4, 1.5, Number.NaN])('ignora a página %s, fora dos limites', async (page) => {
      const { store, http } = setup();
      await loaded(store, http, [resident()], 120);

      await store.goToPage(page);
      await flushPromises();

      http.expectNone('/api/residents');
      expect(store.query().page).toBe(1);
    });
  });

  describe('estado derivado', () => {
    it('conta as páginas pelo total e pelo tamanho da página', async () => {
      const { store, http } = setup();

      await loaded(store, http, [resident()], 101);

      expect(store.pageCount()).toBe(3);
    });

    it('tem ao menos uma página, mesmo sem moradores', async () => {
      const { store, http } = setup();

      await loaded(store, http, [], 0);

      expect(store.pageCount()).toBe(1);
    });

    it('só diz que há filtros quando a situação não é a padrão ou há busca', async () => {
      const { store, http } = setup();
      await loaded(store, http);
      expect(store.hasFilters()).toBe(false);

      const former = store.filter({ status: 'former' });
      listRequest(http).flush(residentPage([]));
      await former;
      expect(store.hasFilters()).toBe(true);

      const back = store.filter({ status: 'active', search: '   ' });
      listRequest(http).flush(residentPage([]));
      await back;
      expect(store.hasFilters()).toBe(false);

      const searching = store.filter({ search: 'ana' });
      listRequest(http).flush(residentPage([]));
      await searching;
      expect(store.hasFilters()).toBe(true);
    });
  });

  describe('markInvited', () => {
    it('troca só a situação do acesso do morador convidado, sem nova busca', async () => {
      const { store, http } = setup();
      const invited = resident();
      const other = resident();
      await loaded(store, http, [invited, other]);

      store.markInvited(invited.id);

      expect(store.items().map((item) => item.accessStatus)).toEqual(['invited', 'none']);
      expect(store.items()[0]).toEqual({ ...invited, accessStatus: 'invited' });
      expect(store.items()[1]).toBe(other);
    });

    it('ignora um id que não está na página', async () => {
      const { store, http } = setup();
      const items = [resident()];
      await loaded(store, http, items);

      store.markInvited('outro-id');

      expect(store.items()).toEqual(items);
    });
  });
});
