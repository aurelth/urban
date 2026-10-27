import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  ActivatedRouteSnapshot,
  CanActivateFn,
  Router,
  RouterStateSnapshot,
  UrlTree,
  provideRouter,
} from '@angular/router';
import { flushPromises } from '../../testing/async';
import { MANAGER, problem, signInAs, tokenResponse } from '../../testing/auth.fixtures';
import { authGuard, guestGuard } from './auth.guards';
import { SessionStore } from './session.store';

function setup(): {
  store: InstanceType<typeof SessionStore>;
  http: HttpTestingController;
  router: Router;
  run: (guard: CanActivateFn, url?: string) => Promise<boolean | UrlTree>;
} {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
  });
  const router = TestBed.inject(Router);
  return {
    store: TestBed.inject(SessionStore),
    http: TestBed.inject(HttpTestingController),
    router,
    run: (guard, url = '/dashboard') =>
      TestBed.runInInjectionContext(
        () =>
          guard({} as ActivatedRouteSnapshot, { url } as RouterStateSnapshot) as Promise<
            boolean | UrlTree
          >,
      ),
  };
}

async function becomeAnonymous(
  store: InstanceType<typeof SessionStore>,
  http: HttpTestingController,
): Promise<void> {
  const restoring = store.restore();
  http
    .expectOne('/api/auth/refresh')
    .flush(problem(401, 'x'), { status: 401, statusText: 'Unauthorized' });
  await restoring;
}

describe('authGuard', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('deixa passar quem esta logado', async () => {
    const { store, http, run } = setup();
    await signInAs(store, http);

    await expect(run(authGuard)).resolves.toBe(true);
  });

  it('manda quem esta anonimo para a entrada, guardando o destino', async () => {
    const { store, http, router, run } = setup();
    await becomeAnonymous(store, http);

    const result = await run(authGuard, '/moradores?pagina=2');

    expect(router.serializeUrl(result as UrlTree)).toBe(
      '/entrar?returnUrl=%2Fmoradores%3Fpagina%3D2',
    );
  });

  it('espera a restauracao da sessao antes de decidir', async () => {
    const { store, http, run } = setup();
    const restoring = store.restore();
    const decision = run(authGuard);
    let decided = false;
    void decision.then(() => (decided = true));

    await flushPromises();
    expect(decided).toBe(false);

    http.expectOne('/api/auth/refresh').flush(tokenResponse());
    await flushPromises();
    http.expectOne('/api/auth/me').flush(MANAGER);
    await restoring;

    await expect(decision).resolves.toBe(true);
  });
});

describe('guestGuard', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('deixa passar quem nao esta logado', async () => {
    const { store, http, run } = setup();
    await becomeAnonymous(store, http);

    await expect(run(guestGuard, '/entrar')).resolves.toBe(true);
  });

  it('manda quem ja esta logado para o painel', async () => {
    const { store, http, router, run } = setup();
    await signInAs(store, http);

    const result = await run(guestGuard, '/entrar');

    expect(router.serializeUrl(result as UrlTree)).toBe('/dashboard');
  });
});
