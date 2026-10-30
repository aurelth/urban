import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { jest } from '@jest/globals';
import { MANAGER, REGISTERED_MANAGER, signInAs } from '../../testing/auth.fixtures';
import { CurrentUser } from '../auth/auth.models';
import { SessionStore } from '../auth/session.store';
import { provideUrbanIcons } from '../theme/icons';
import { ShellLayout } from './shell.layout';

async function setup(user: CurrentUser = MANAGER): Promise<{
  fixture: ComponentFixture<ShellLayout>;
  store: InstanceType<typeof SessionStore>;
  http: HttpTestingController;
  router: Router;
  html: HTMLElement;
}> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
    ],
  });
  const store = TestBed.inject(SessionStore);
  const http = TestBed.inject(HttpTestingController);
  await signInAs(store, http, user);
  const fixture = TestBed.createComponent(ShellLayout);
  await fixture.whenStable();
  return { fixture, store, http, router: TestBed.inject(Router), html: fixture.nativeElement };
}

describe('ShellLayout', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('mostra o e-mail e o papel enquanto a pessoa nao escolheu um nome', async () => {
    const { html } = await setup();
    const account = html.querySelector('[data-testid="current-user"]')?.textContent;

    expect(account).toContain('sindica@example.com');
    expect(account).toContain('Síndico');
    expect(account).not.toContain('Olá');
  });

  it('cumprimenta pelo nome escolhido e mostra o papel, sem o e-mail', async () => {
    const { html } = await setup(REGISTERED_MANAGER);
    const account = html.querySelector('[data-testid="current-user"]')?.textContent;

    expect(account).toContain('Olá Marina!');
    expect(account).toContain('Síndico');
    expect(account).not.toContain('sindica@example.com');
  });

  it('mantem o botao de tema', async () => {
    const { html } = await setup();

    expect(html.querySelector('button[aria-label="Usar tema escuro"]')).not.toBeNull();
  });

  it('sai pela API e volta para a entrada', async () => {
    const { fixture, http, router, html } = await setup();
    const navigate = jest.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    html.querySelector<HTMLButtonElement>('button[data-testid="sign-out"]')!.click();
    http.expectOne('/api/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith('/entrar');
  });

  it('leva para a entrada, guardando o destino, quando a sessao expira', async () => {
    const { fixture, store, router } = await setup();
    const navigate = jest.spyOn(router, 'navigate').mockResolvedValue(true);

    store.expire();
    TestBed.tick();
    await fixture.whenStable();

    expect(navigate).toHaveBeenCalledWith(['/entrar'], { queryParams: { returnUrl: router.url } });
  });

  it('nao guarda destino quando a pessoa saiu de proposito', async () => {
    const { fixture, http, router, html } = await setup();
    const navigate = jest.spyOn(router, 'navigate').mockResolvedValue(true);
    jest.spyOn(router, 'navigateByUrl').mockResolvedValue(true);

    html.querySelector<HTMLButtonElement>('button[data-testid="sign-out"]')!.click();
    http.expectOne('/api/auth/logout').flush(null, { status: 204, statusText: 'No Content' });
    await fixture.whenStable();
    TestBed.tick();

    expect(navigate).not.toHaveBeenCalled();
  });

  describe('links do cabecalho', () => {
    function link(html: HTMLElement, name: string): HTMLAnchorElement | null {
      return html.querySelector<HTMLAnchorElement>(`a[data-testid="${name}"]`);
    }

    it('leva o sindico com condominio aos dados do condominio e a conta', async () => {
      const { html } = await setup(REGISTERED_MANAGER);

      expect(link(html, 'nav-profile')?.textContent?.trim()).toBe('Meus dados');
      expect(link(html, 'nav-profile')?.getAttribute('href')).toBe('/condominio/meus-dados');
      expect(link(html, 'nav-account')?.textContent?.trim()).toBe('Minha conta');
      expect(link(html, 'nav-account')?.getAttribute('href')).toBe('/conta');
    });

    it('nao mostra os dados do condominio ao sindico que ainda nao o cadastrou', async () => {
      const { html } = await setup(MANAGER);

      expect(link(html, 'nav-profile')).toBeNull();
      expect(link(html, 'nav-account')).not.toBeNull();
    });

    it.each([
      ['morador', 'Resident' as const],
      ['administrador', 'SuperAdmin' as const],
    ])('mostra so a conta para %s', async (_name, role) => {
      const { html } = await setup({ ...REGISTERED_MANAGER, role });

      expect(link(html, 'nav-profile')).toBeNull();
      expect(link(html, 'nav-account')).not.toBeNull();
    });
  });
});
