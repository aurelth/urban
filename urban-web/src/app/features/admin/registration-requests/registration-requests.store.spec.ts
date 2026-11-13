import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { flushPromises } from '../../../testing/async';
import { problem } from '../../../testing/auth.fixtures';
import {
  REGISTRATION_REQUESTS_URL,
  SECOND_REQUEST,
  registrationRequest,
} from '../../../testing/registration-requests.fixtures';
import { RegistrationRequestsStore } from './registration-requests.store';

const FIRST = registrationRequest();
const NO_CONTENT = { status: 204, statusText: 'No Content' };

function setup(): {
  store: InstanceType<typeof RegistrationRequestsStore>;
  http: HttpTestingController;
} {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), RegistrationRequestsStore],
  });
  return {
    store: TestBed.inject(RegistrationRequestsStore),
    http: TestBed.inject(HttpTestingController),
  };
}

function fail(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

async function loaded(
  store: InstanceType<typeof RegistrationRequestsStore>,
  http: HttpTestingController,
  items = [FIRST, SECOND_REQUEST],
): Promise<void> {
  const loading = store.load();
  http.expectOne(REGISTRATION_REQUESTS_URL).flush(items);
  await loading;
}

describe('RegistrationRequestsStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('comeca carregando, sem pedidos', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.items()).toEqual([]);
    expect(store.busy()).toBe(false);
  });

  it('mostra os pedidos que a API devolve', async () => {
    const { store, http } = setup();

    await loaded(store, http);

    expect(store.status()).toBe('ready');
    expect(store.items()).toEqual([FIRST, SECOND_REQUEST]);
  });

  it('fica pronto e vazio quando nao ha pedidos', async () => {
    const { store, http } = setup();

    await loaded(store, http, []);

    expect(store.status()).toBe('ready');
    expect(store.items()).toEqual([]);
  });

  it('avisa o erro quando a busca falha e deixa tentar de novo', async () => {
    const { store, http } = setup();
    const first = store.load();
    http.expectOne(REGISTRATION_REQUESTS_URL).flush(...fail(500, 'x'));
    await first;
    expect(store.status()).toBe('error');

    const retry = store.load();
    expect(store.status()).toBe('loading');
    http.expectOne(REGISTRATION_REQUESTS_URL).flush([FIRST]);
    await retry;

    expect(store.status()).toBe('ready');
    expect(store.items()).toEqual([FIRST]);
  });

  describe.each([
    ['approve', 'aprova'],
    ['reject', 'recusa'],
  ] as const)('%s', (action, verb) => {
    it(`${verb} o pedido e busca a lista de novo`, async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const deciding = store[action](FIRST);
      expect(store.busy()).toBe(true);
      http.expectOne(`${REGISTRATION_REQUESTS_URL}/${FIRST.id}/${action}`).flush(null, NO_CONTENT);
      await flushPromises();
      http.expectOne(REGISTRATION_REQUESTS_URL).flush([SECOND_REQUEST]);
      const result = await deciding;

      expect(result).toBeNull();
      expect(store.items()).toEqual([SECOND_REQUEST]);
      expect(store.busy()).toBe(false);
    });

    it.each([
      [404, 'identity.registration_request.not_found'],
      [409, 'identity.user.invalid_transition'],
    ])(
      'busca a lista de novo quando a API responde %i (tela desatualizada)',
      async (status, code) => {
        const { store, http } = setup();
        await loaded(store, http);

        const deciding = store[action](FIRST);
        http
          .expectOne(`${REGISTRATION_REQUESTS_URL}/${FIRST.id}/${action}`)
          .flush(...fail(status, code));
        await flushPromises();
        http.expectOne(REGISTRATION_REQUESTS_URL).flush([SECOND_REQUEST]);
        const result = await deciding;

        expect(result?.code).toBe(code);
        expect(store.items()).toEqual([SECOND_REQUEST]);
        expect(store.busy()).toBe(false);
      },
    );

    it('devolve o problema sem buscar a lista quando a API recusa por outro motivo', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const deciding = store[action](FIRST);
      http
        .expectOne(`${REGISTRATION_REQUESTS_URL}/${FIRST.id}/${action}`)
        .flush(...fail(403, 'identity.registration_request.forbidden'));
      const result = await deciding;

      expect(result?.status).toBe(403);
      expect(store.items()).toEqual([FIRST, SECOND_REQUEST]);
      expect(store.busy()).toBe(false);
    });

    it('vai para o estado de erro se a lista nao puder ser buscada depois da decisao', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const deciding = store[action](FIRST);
      http.expectOne(`${REGISTRATION_REQUESTS_URL}/${FIRST.id}/${action}`).flush(null, NO_CONTENT);
      await flushPromises();
      http.expectOne(REGISTRATION_REQUESTS_URL).flush(...fail(500, 'x'));
      await deciding;

      expect(store.status()).toBe('error');
      expect(store.busy()).toBe(false);
    });
  });
});
