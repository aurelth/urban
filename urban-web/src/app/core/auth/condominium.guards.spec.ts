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
import {
  MANAGER,
  REGISTERED_MANAGER,
  problem,
  signInAs,
  tokenResponse,
} from '../../testing/auth.fixtures';
import { CurrentUser } from './auth.models';
import { condominiumGuard, condominiumSetupGuard } from './condominium.guards';
import { SessionStore } from './session.store';

function setup(): {
  store: InstanceType<typeof SessionStore>;
  http: HttpTestingController;
  router: Router;
  run: (guard: CanActivateFn) => Promise<boolean | UrlTree>;
} {
  TestBed.configureTestingModule({
    providers: [provideRouter([]), provideHttpClient(), provideHttpClientTesting()],
  });
  return {
    store: TestBed.inject(SessionStore),
    http: TestBed.inject(HttpTestingController),
    router: TestBed.inject(Router),
    run: (guard) =>
      TestBed.runInInjectionContext(
        () =>
          guard(
            {} as ActivatedRouteSnapshot,
            { url: '/dashboard' } as RouterStateSnapshot,
          ) as Promise<boolean | UrlTree>,
      ),
  };
}

const RESIDENT: CurrentUser = { ...REGISTERED_MANAGER, role: 'Resident' };
const ADMIN: CurrentUser = { ...MANAGER, role: 'SuperAdmin' };

describe('condominiumGuard', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('leva o síndico que ainda não cadastrou o condomínio para o formulário', async () => {
    const { store, http, router, run } = setup();
    await signInAs(store, http, MANAGER);

    const result = await run(condominiumGuard);

    expect(router.serializeUrl(result as UrlTree)).toBe('/condominio/cadastrar');
  });

  it.each([
    ['síndico com condomínio', REGISTERED_MANAGER],
    ['morador', RESIDENT],
    ['administrador', ADMIN],
  ])('deixa passar %s', async (_name, user) => {
    const { store, http, run } = setup();
    await signInAs(store, http, user);

    await expect(run(condominiumGuard)).resolves.toBe(true);
  });

  it('deixa a decisão sobre quem não está logado para a guarda de login', async () => {
    const { store, http, run } = setup();
    const restoring = store.restore();
    http.expectOne('/api/auth/refresh').flush(problem(401, 'x'), {
      status: 401,
      statusText: 'Unauthorized',
    });
    await restoring;

    await expect(run(condominiumGuard)).resolves.toBe(true);
  });

  it('espera a restauração da sessão antes de decidir', async () => {
    const { store, http, router, run } = setup();
    const restoring = store.restore();
    const decision = run(condominiumGuard);
    let decided = false;
    void decision.then(() => (decided = true));

    await flushPromises();
    expect(decided).toBe(false);

    http.expectOne('/api/auth/refresh').flush(tokenResponse());
    await flushPromises();
    http.expectOne('/api/auth/me').flush(MANAGER);
    await restoring;

    expect(router.serializeUrl((await decision) as UrlTree)).toBe('/condominio/cadastrar');
  });
});

describe('condominiumSetupGuard', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('abre o formulário para o síndico que ainda não cadastrou o condomínio', async () => {
    const { store, http, run } = setup();
    await signInAs(store, http, MANAGER);

    await expect(run(condominiumSetupGuard)).resolves.toBe(true);
  });

  it.each([
    ['síndico com condomínio', REGISTERED_MANAGER],
    ['morador', RESIDENT],
    ['administrador', ADMIN],
  ])('manda %s para o painel', async (_name, user) => {
    const { store, http, router, run } = setup();
    await signInAs(store, http, user);

    const result = await run(condominiumSetupGuard);

    expect(router.serializeUrl(result as UrlTree)).toBe('/dashboard');
  });

  it('espera a restauração da sessão antes de decidir', async () => {
    const { store, http, run } = setup();
    const restoring = store.restore();
    const decision = run(condominiumSetupGuard);
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
