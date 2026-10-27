import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { firstValueFrom } from 'rxjs';
import { flushPromises } from '../../testing/async';
import { problem, signInAs, tokenResponse } from '../../testing/auth.fixtures';
import { authInterceptor } from './auth.interceptor';
import { SessionStore } from './session.store';

const UNAUTHORIZED = { status: 401, statusText: 'Unauthorized' };

function setup(): {
  store: InstanceType<typeof SessionStore>;
  http: HttpTestingController;
  client: HttpClient;
} {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(withInterceptors([authInterceptor])), provideHttpClientTesting()],
  });
  return {
    store: TestBed.inject(SessionStore),
    http: TestBed.inject(HttpTestingController),
    client: TestBed.inject(HttpClient),
  };
}

describe('authInterceptor', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it.each([
    '/api/auth/login',
    '/api/auth/refresh',
    '/api/auth/register',
    '/api/auth/confirm-email',
    '/api/auth/forgot-password',
    '/api/auth/reset-password',
    '/api/terms/current',
    '/api/terms/current?idioma=pt',
  ])('nao envia o token para a rota publica %s', async (url) => {
    const { store, http, client } = setup();
    await signInAs(store, http);

    const response = firstValueFrom(client.post(url, {}));
    const request = http.expectOne(url);
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
    await response;
  });

  it.each(['/api/auth/me', '/api/auth/logout', '/api/condominios'])(
    'envia o token para %s',
    async (url) => {
      const { store, http, client } = setup();
      await signInAs(store, http);

      const response = firstValueFrom(client.get(url));
      const request = http.expectOne(url);
      expect(request.request.headers.get('Authorization')).toBe('Bearer access-1');
      request.flush({});
      await response;
    },
  );

  it('ja envia o token na chamada que carrega o usuario logo apos o login', async () => {
    const { store, http } = setup();

    const signingIn = store.signIn('sindica@example.com', 'Senha-segura-123');
    http.expectOne('/api/auth/login').flush(tokenResponse('access-5'));
    await flushPromises();
    const me = http.expectOne('/api/auth/me');
    expect(me.request.headers.get('Authorization')).toBe('Bearer access-5');
    me.flush({ id: 'x', email: 'a@b.c', role: 'Resident', condominiumId: 'y' });
    await signingIn;
  });

  it('nao inventa o cabecalho quando ainda nao ha token', async () => {
    const { http, client } = setup();

    const response = firstValueFrom(client.get('/api/condominios'));
    const request = http.expectOne('/api/condominios');
    expect(request.request.headers.has('Authorization')).toBe(false);
    request.flush({});
    await response;
  });

  it('nao mexe em chamadas fora da API', async () => {
    const { store, http, client } = setup();
    await signInAs(store, http);

    const local = firstValueFrom(client.get('/assets/dados.json'));
    const remote = firstValueFrom(client.get('https://outro.example/api/condominios'));
    const localRequest = http.expectOne('/assets/dados.json');
    const remoteRequest = http.expectOne('https://outro.example/api/condominios');
    expect(localRequest.request.headers.has('Authorization')).toBe(false);
    expect(remoteRequest.request.headers.has('Authorization')).toBe(false);
    localRequest.flush({});
    remoteRequest.flush({});
    await Promise.all([local, remote]);
  });

  it('renova a sessao e repete a chamada uma vez quando recebe 401', async () => {
    const { store, http, client } = setup();
    await signInAs(store, http);

    const result = firstValueFrom(client.get<{ ok: boolean }>('/api/condominios'));
    http
      .expectOne('/api/condominios')
      .flush(problem(401, 'identity.session.unauthenticated'), UNAUTHORIZED);
    http.expectOne('/api/auth/refresh').flush(tokenResponse('access-2'));
    await flushPromises();
    const retry = http.expectOne('/api/condominios');
    expect(retry.request.headers.get('Authorization')).toBe('Bearer access-2');
    retry.flush({ ok: true });

    await expect(result).resolves.toEqual({ ok: true });
    expect(store.accessToken()).toBe('access-2');
  });

  it('derruba a sessao e repassa o 401 quando a renovacao falha', async () => {
    const { store, http, client } = setup();
    await signInAs(store, http);

    const result = firstValueFrom(client.get('/api/condominios'));
    http
      .expectOne('/api/condominios')
      .flush(problem(401, 'identity.session.unauthenticated'), UNAUTHORIZED);
    http
      .expectOne('/api/auth/refresh')
      .flush(problem(401, 'identity.refresh_token.expired'), UNAUTHORIZED);

    await expect(result).rejects.toMatchObject({ status: 401 });
    expect(store.status()).toBe('anonymous');
    expect(store.expired()).toBe(true);
  });

  it('nao marca expiracao quando quem recebeu 401 ja estava anonimo', async () => {
    const { store, http, client } = setup();

    const result = firstValueFrom(client.get('/api/condominios'));
    http
      .expectOne('/api/condominios')
      .flush(problem(401, 'identity.session.unauthenticated'), UNAUTHORIZED);
    http.expectOne('/api/auth/refresh').flush(problem(401, 'x'), UNAUTHORIZED);

    await expect(result).rejects.toMatchObject({ status: 401 });
    expect(store.expired()).toBe(false);
  });

  it('nao renova nem repete mais de uma vez', async () => {
    const { store, http, client } = setup();
    await signInAs(store, http);

    const result = firstValueFrom(client.get('/api/condominios'));
    http.expectOne('/api/condominios').flush(problem(401, 'x'), UNAUTHORIZED);
    http.expectOne('/api/auth/refresh').flush(tokenResponse('access-2'));
    await flushPromises();
    http.expectOne('/api/condominios').flush(problem(401, 'x'), UNAUTHORIZED);

    await expect(result).rejects.toMatchObject({ status: 401 });
    http.expectNone('/api/auth/refresh');
  });

  it('faz uma unica renovacao quando varias chamadas recebem 401 juntas', async () => {
    const { store, http, client } = setup();
    await signInAs(store, http);

    const first = firstValueFrom(client.get('/api/condominios'));
    const second = firstValueFrom(client.get('/api/moradores'));
    http.expectOne('/api/condominios').flush(problem(401, 'x'), UNAUTHORIZED);
    http.expectOne('/api/moradores').flush(problem(401, 'x'), UNAUTHORIZED);
    http.expectOne('/api/auth/refresh').flush(tokenResponse('access-2'));
    await flushPromises();
    http.expectOne('/api/condominios').flush({});
    http.expectOne('/api/moradores').flush({});

    await Promise.all([first, second]);
  });

  it.each(['/api/auth/login', '/api/auth/confirm-email'])(
    'repassa o 401 de %s sem tentar renovar',
    async (url) => {
      const { http, client } = setup();

      const result = firstValueFrom(client.post(url, {}));
      http.expectOne(url).flush(problem(401, 'identity.login.invalid_credentials'), UNAUTHORIZED);

      await expect(result).rejects.toMatchObject({ status: 401 });
      http.expectNone('/api/auth/refresh');
    },
  );

  it.each([400, 403, 500, 503])('nao tenta renovar quando recebe %i', async (status) => {
    const { store, http, client } = setup();
    await signInAs(store, http);

    const result = firstValueFrom(client.get('/api/condominios'));
    http.expectOne('/api/condominios').flush(problem(status, 'x'), { status, statusText: 'Erro' });

    await expect(result).rejects.toMatchObject({ status });
    http.expectNone('/api/auth/refresh');
    expect(store.status()).toBe('authenticated');
  });
});
