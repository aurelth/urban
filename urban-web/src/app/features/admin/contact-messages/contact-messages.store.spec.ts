import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { flushPromises } from '../../../testing/async';
import { problem } from '../../../testing/auth.fixtures';
import {
  CONTACT_MESSAGES_URL,
  READ_MESSAGE,
  contactMessage,
  contactMessagesPage,
} from '../../../testing/contact.fixtures';
import { ContactMessagesStore } from './contact-messages.store';

type Store = InstanceType<typeof ContactMessagesStore>;

const FIRST = contactMessage();
const NO_CONTENT = { status: 204, statusText: 'No Content' };

function setup(): { store: Store; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), ContactMessagesStore],
  });
  return {
    store: TestBed.inject(ContactMessagesStore),
    http: TestBed.inject(HttpTestingController),
  };
}

function listRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((r) => r.url === CONTACT_MESSAGES_URL && r.method === 'GET');
}

function params(request: TestRequest): string {
  return request.request.params.toString();
}

function fail(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

async function loaded(
  store: Store,
  http: HttpTestingController,
  items = [FIRST, READ_MESSAGE],
  total = items.length,
): Promise<void> {
  const loading = store.load();
  listRequest(http).flush(contactMessagesPage(items, total));
  await loading;
}

describe('ContactMessagesStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('começa carregando, sem mensagens e sem filtro', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.items()).toEqual([]);
    expect(store.busy()).toBe(false);
    expect(store.hasFilters()).toBe(false);
    expect(store.pageCount()).toBe(1);
  });

  it('mostra as mensagens que a API devolve, da primeira página', async () => {
    const { store, http } = setup();
    const loading = store.load();

    const request = listRequest(http);
    expect(params(request)).toBe('page=1&pageSize=20');
    request.flush(contactMessagesPage([FIRST, READ_MESSAGE], 2));
    await loading;

    expect(store.status()).toBe('ready');
    expect(store.items()).toEqual([FIRST, READ_MESSAGE]);
    expect(store.total()).toBe(2);
  });

  it('fica em erro quando a busca falha e carrega ao tentar de novo', async () => {
    const { store, http } = setup();
    const first = store.load();
    listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
    await first;
    expect(store.status()).toBe('error');

    const retry = store.load();
    expect(store.status()).toBe('loading');
    listRequest(http).flush(contactMessagesPage([FIRST]));
    await retry;

    expect(store.status()).toBe('ready');
  });

  it('calcula o número de páginas pelo total', async () => {
    const { store, http } = setup();

    await loaded(store, http, [FIRST], 45);

    expect(store.pageCount()).toBe(3);
  });

  describe('filtro', () => {
    it('busca só as não lidas, volta à primeira página e marca que há filtro', async () => {
      const { store, http } = setup();
      await loaded(store, http, [FIRST], 45);
      const next = store.goToPage(2);
      listRequest(http).flush(contactMessagesPage([FIRST], 45, 2));
      await next;

      const filtering = store.filter({ status: 'unread' });
      expect(store.busy()).toBe(true);
      const request = listRequest(http);
      expect(params(request)).toBe('status=unread&page=1&pageSize=20');
      request.flush(contactMessagesPage([FIRST], 1));
      await filtering;

      expect(store.query().status).toBe('unread');
      expect(store.query().page).toBe(1);
      expect(store.hasFilters()).toBe(true);
      expect(store.busy()).toBe(false);
    });

    it('fica em erro quando a busca filtrada falha', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const filtering = store.filter({ status: 'unread' });
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await filtering;

      expect(store.status()).toBe('error');
      expect(store.busy()).toBe(false);
    });
  });

  describe('página', () => {
    it('vai para a página pedida', async () => {
      const { store, http } = setup();
      await loaded(store, http, [FIRST], 45);

      const going = store.goToPage(3);
      const request = listRequest(http);
      expect(params(request)).toBe('page=3&pageSize=20');
      request.flush(contactMessagesPage([FIRST], 45, 3));
      await going;

      expect(store.query().page).toBe(3);
    });

    it.each([0, 4, 1.5, Number.NaN])('ignora a página %s, fora do intervalo', async (page) => {
      const { store, http } = setup();
      await loaded(store, http, [FIRST], 45);

      await store.goToPage(page);

      http.expectNone(CONTACT_MESSAGES_URL);
      expect(store.query().page).toBe(1);
    });
  });

  describe('marcar como lida', () => {
    it('chama a API, busca a lista de novo e devolve nulo', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const marking = store.markRead(FIRST);
      expect(store.busy()).toBe(true);
      http.expectOne(`${CONTACT_MESSAGES_URL}/${FIRST.id}/read`).flush(null, NO_CONTENT);
      await flushPromises();
      listRequest(http).flush(
        contactMessagesPage([{ ...FIRST, readAt: '2026-11-13T16:00:00+00:00' }, READ_MESSAGE]),
      );

      expect(await marking).toBeNull();
      expect(store.items()[0].readAt).toBe('2026-11-13T16:00:00+00:00');
      expect(store.busy()).toBe(false);
    });

    it('volta uma página quando a atual ficou vazia', async () => {
      const { store, http } = setup();
      await loaded(store, http, [FIRST], 21);
      const going = store.goToPage(2);
      listRequest(http).flush(contactMessagesPage([FIRST], 21, 2));
      await going;

      const marking = store.markRead(FIRST);
      http.expectOne(`${CONTACT_MESSAGES_URL}/${FIRST.id}/read`).flush(null, NO_CONTENT);
      await flushPromises();
      listRequest(http).flush(contactMessagesPage([], 20, 2));
      await flushPromises();
      const back = listRequest(http);
      expect(params(back)).toBe('page=1&pageSize=20');
      back.flush(contactMessagesPage([FIRST], 20));

      await marking;
      expect(store.query().page).toBe(1);
    });

    it('busca a lista de novo e devolve o problema quando a mensagem sumiu (404)', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const marking = store.markRead(FIRST);
      http
        .expectOne(`${CONTACT_MESSAGES_URL}/${FIRST.id}/read`)
        .flush(...fail(404, 'identity.contact.message_not_found'));
      await flushPromises();
      listRequest(http).flush(contactMessagesPage([READ_MESSAGE]));

      const result = await marking;
      expect(result?.code).toBe('identity.contact.message_not_found');
      expect(store.items()).toEqual([READ_MESSAGE]);
    });

    it.each([
      [403, 'identity.contact.read_forbidden'],
      [500, 'identity.internal'],
    ])('só devolve o problema, sem buscar de novo, no status %s', async (status, code) => {
      const { store, http } = setup();
      await loaded(store, http);

      const marking = store.markRead(FIRST);
      http.expectOne(`${CONTACT_MESSAGES_URL}/${FIRST.id}/read`).flush(...fail(status, code));

      expect((await marking)?.status).toBe(status);
      http.expectNone(CONTACT_MESSAGES_URL);
      expect(store.busy()).toBe(false);
    });

    it('fica em erro quando a busca depois de marcar falha', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const marking = store.markRead(FIRST);
      http.expectOne(`${CONTACT_MESSAGES_URL}/${FIRST.id}/read`).flush(null, NO_CONTENT);
      await flushPromises();
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await marking;

      expect(store.status()).toBe('error');
    });
  });
});
