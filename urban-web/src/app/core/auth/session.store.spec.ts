import { HttpErrorResponse, provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { flushPromises } from '../../testing/async';
import { MANAGER, problem, signInAs, tokenResponse } from '../../testing/auth.fixtures';
import { CurrentUser } from './auth.models';
import { SessionStore } from './session.store';

function setup(): { store: InstanceType<typeof SessionStore>; http: HttpTestingController } {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { store: TestBed.inject(SessionStore), http: TestBed.inject(HttpTestingController) };
}

describe('SessionStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('comeca sem saber quem esta logado', () => {
    const { store } = setup();

    expect(store.status()).toBe('unknown');
    expect(store.user()).toBeNull();
    expect(store.accessToken()).toBeNull();
    expect(store.expired()).toBe(false);
    expect(store.needsCondominium()).toBe(false);
  });

  describe('restore', () => {
    it('renova a sessao pelo cookie e carrega o usuario', async () => {
      const { store, http } = setup();

      const restoring = store.restore();
      http.expectOne('/api/auth/refresh').flush(tokenResponse('access-9'));
      await flushPromises();
      http.expectOne('/api/auth/me').flush(MANAGER);
      await restoring;

      expect(store.status()).toBe('authenticated');
      expect(store.user()).toEqual(MANAGER);
      expect(store.accessToken()).toBe('access-9');
    });

    it('fica anonimo, sem marcar a sessao como expirada, quando nao ha cookie valido', async () => {
      const { store, http } = setup();

      const restoring = store.restore();
      http.expectOne('/api/auth/refresh').flush(problem(401, 'identity.refresh.invalid_session'), {
        status: 401,
        statusText: 'Unauthorized',
      });
      await restoring;

      expect(store.status()).toBe('anonymous');
      expect(store.accessToken()).toBeNull();
      expect(store.expired()).toBe(false);
    });

    it('fica anonimo e descarta o token quando nao consegue carregar o usuario', async () => {
      const { store, http } = setup();

      const restoring = store.restore();
      http.expectOne('/api/auth/refresh').flush(tokenResponse());
      await flushPromises();
      http.expectOne('/api/auth/me').flush(problem(403, 'identity.user.account_suspended'), {
        status: 403,
        statusText: 'Forbidden',
      });
      await restoring;

      expect(store.status()).toBe('anonymous');
      expect(store.accessToken()).toBeNull();
      expect(store.user()).toBeNull();
    });

    it('ready() espera a restauracao terminar', async () => {
      const { store, http } = setup();
      const restoring = store.restore();
      let ready = false;
      void store.ready().then(() => (ready = true));

      await flushPromises();
      expect(ready).toBe(false);

      http
        .expectOne('/api/auth/refresh')
        .flush(problem(401, 'x'), { status: 401, statusText: 'Unauthorized' });
      await restoring;
      await flushPromises();

      expect(ready).toBe(true);
    });

    it('ready() responde na hora quando nenhuma restauracao foi iniciada', async () => {
      const { store } = setup();

      await expect(store.ready()).resolves.toBeUndefined();
    });
  });

  describe('signIn', () => {
    it('entra, carrega o usuario e guarda o token so na memoria', async () => {
      const { store, http } = setup();

      const signingIn = store.signIn('sindica@example.com', 'Senha-segura-123');
      const login = http.expectOne('/api/auth/login');
      expect(login.request.body).toEqual({
        email: 'sindica@example.com',
        password: 'Senha-segura-123',
      });
      login.flush(tokenResponse('access-3'));
      await flushPromises();
      http.expectOne('/api/auth/me').flush(MANAGER);
      await signingIn;

      expect(store.status()).toBe('authenticated');
      expect(store.user()).toEqual(MANAGER);
      expect(store.accessToken()).toBe('access-3');
      expect(store.expired()).toBe(false);
      expect(localStorage.length).toBe(0);
      expect(sessionStorage.length).toBe(0);
    });

    it('repassa a falha de credenciais e nao guarda nada', async () => {
      const { store, http } = setup();

      const signingIn = store.signIn('sindica@example.com', 'errada');
      http.expectOne('/api/auth/login').flush(problem(401, 'identity.login.invalid_credentials'), {
        status: 401,
        statusText: 'Unauthorized',
      });

      await expect(signingIn).rejects.toBeInstanceOf(HttpErrorResponse);
      expect(store.status()).not.toBe('authenticated');
      expect(store.accessToken()).toBeNull();
    });

    it('descarta o token quando nao consegue carregar o usuario', async () => {
      const { store, http } = setup();

      const signingIn = store.signIn('sindica@example.com', 'Senha-segura-123');
      http.expectOne('/api/auth/login').flush(tokenResponse());
      await flushPromises();
      http.expectOne('/api/auth/me').flush(problem(503, 'identity.session.store_unavailable'), {
        status: 503,
        statusText: 'Service Unavailable',
      });

      await expect(signingIn).rejects.toBeInstanceOf(HttpErrorResponse);
      expect(store.accessToken()).toBeNull();
      expect(store.status()).not.toBe('authenticated');
    });

    it('limpa a marca de sessao expirada ao entrar de novo', async () => {
      const { store, http } = setup();
      await signInAs(store, http);
      store.expire();
      expect(store.expired()).toBe(true);

      await signInAs(store, http);

      expect(store.expired()).toBe(false);
      expect(store.status()).toBe('authenticated');
    });
  });

  describe('signOut', () => {
    it('avisa a API e limpa a sessao', async () => {
      const { store, http } = setup();
      await signInAs(store, http);

      const signingOut = store.signOut();
      http.expectOne('/api/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
      await signingOut;

      expect(store.status()).toBe('anonymous');
      expect(store.user()).toBeNull();
      expect(store.accessToken()).toBeNull();
      expect(store.expired()).toBe(false);
    });

    it('limpa a sessao mesmo quando a API falha', async () => {
      const { store, http } = setup();
      await signInAs(store, http);

      const signingOut = store.signOut();
      http
        .expectOne('/api/auth/logout')
        .flush(null, { status: 503, statusText: 'Service Unavailable' });
      await signingOut;

      expect(store.status()).toBe('anonymous');
      expect(store.accessToken()).toBeNull();
    });
  });

  describe('renew', () => {
    it('renova o token', async () => {
      const { store, http } = setup();

      const renewing = store.renew();
      http.expectOne('/api/auth/refresh').flush(tokenResponse('access-7'));

      await expect(renewing).resolves.toBe(true);
      expect(store.accessToken()).toBe('access-7');
    });

    it('faz uma unica chamada quando varias renovacoes chegam juntas', async () => {
      const { store, http } = setup();

      const first = store.renew();
      const second = store.renew();
      http.expectOne('/api/auth/refresh').flush(tokenResponse('access-8'));

      await expect(Promise.all([first, second])).resolves.toEqual([true, true]);
    });

    it('faz uma nova chamada quando a anterior ja terminou', async () => {
      const { store, http } = setup();

      const first = store.renew();
      http.expectOne('/api/auth/refresh').flush(tokenResponse('access-1'));
      await first;
      const second = store.renew();
      http.expectOne('/api/auth/refresh').flush(tokenResponse('access-2'));
      await second;

      expect(store.accessToken()).toBe('access-2');
    });

    it('devolve false e mantem o token quando a renovacao falha', async () => {
      const { store, http } = setup();
      await signInAs(store, http);

      const renewing = store.renew();
      http.expectOne('/api/auth/refresh').flush(problem(401, 'identity.refresh_token.expired'), {
        status: 401,
        statusText: 'Unauthorized',
      });

      await expect(renewing).resolves.toBe(false);
      expect(store.accessToken()).toBe('access-1');
    });
  });

  describe('expire', () => {
    it('derruba a sessao e marca que ela expirou', async () => {
      const { store, http } = setup();
      await signInAs(store, http);

      store.expire();

      expect(store.status()).toBe('anonymous');
      expect(store.user()).toBeNull();
      expect(store.accessToken()).toBeNull();
      expect(store.expired()).toBe(true);
    });

    it('nao marca expiracao para quem ja estava anonimo', async () => {
      const { store, http } = setup();
      const restoring = store.restore();
      http
        .expectOne('/api/auth/refresh')
        .flush(problem(401, 'x'), { status: 401, statusText: 'Unauthorized' });
      await restoring;

      store.expire();

      expect(store.expired()).toBe(false);
    });
  });

  describe('needsCondominium', () => {
    const cases: [string, CurrentUser, boolean][] = [
      ['sindico sem condominio', { ...MANAGER, condominiumId: null }, true],
      ['sindico com condominio', { ...MANAGER, condominiumId: '0198-condominio' }, false],
      [
        'administrador sem condominio',
        { ...MANAGER, role: 'SuperAdmin', condominiumId: null },
        false,
      ],
      [
        'morador com condominio',
        { ...MANAGER, role: 'Resident', condominiumId: '0198-condominio' },
        false,
      ],
    ];

    it.each(cases)('%s', async (_name, user, expected) => {
      const { store, http } = setup();

      await signInAs(store, http, user);

      expect(store.needsCondominium()).toBe(expected);
    });
  });
});
