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
import { adminGuard } from './admin.guards';
import { CurrentUser } from './auth.models';
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
            { url: '/admin/pedidos-de-cadastro' } as RouterStateSnapshot,
          ) as Promise<boolean | UrlTree>,
      ),
  };
}

const ADMIN: CurrentUser = { ...MANAGER, role: 'SuperAdmin' };
const RESIDENT: CurrentUser = { ...REGISTERED_MANAGER, role: 'Resident' };

describe('adminGuard', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('deixa o administrador entrar nas telas de administracao', async () => {
    const { store, http, run } = setup();
    await signInAs(store, http, ADMIN);

    await expect(run(adminGuard)).resolves.toBe(true);
  });

  it.each([
    ['sindico com condominio', REGISTERED_MANAGER],
    ['sindico sem condominio', MANAGER],
    ['morador', RESIDENT],
  ])('leva %s para o painel', async (_name, user) => {
    const { store, http, router, run } = setup();
    await signInAs(store, http, user);

    const result = await run(adminGuard);

    expect(router.serializeUrl(result as UrlTree)).toBe('/dashboard');
  });

  it('espera a restauracao da sessao antes de decidir', async () => {
    const { store, http, run } = setup();
    const restoring = store.restore();
    const decision = run(adminGuard);
    let decided = false;
    void decision.then(() => (decided = true));

    await flushPromises();
    expect(decided).toBe(false);

    http.expectOne('/api/auth/refresh').flush(tokenResponse());
    await flushPromises();
    http.expectOne('/api/auth/me').flush(ADMIN);
    await restoring;

    await expect(decision).resolves.toBe(true);
  });

  it('deixa a decisao sobre quem nao esta logado para a guarda de login', async () => {
    const { store, http, run } = setup();
    const restoring = store.restore();
    await flushPromises();
    http.expectOne('/api/auth/refresh').flush(problem(401, 'identity.session.unauthenticated'), {
      status: 401,
      statusText: 'Unauthorized',
    });
    await restoring;

    await expect(run(adminGuard)).resolves.toBe(true);
  });
});
