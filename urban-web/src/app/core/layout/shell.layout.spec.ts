import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { jest } from '@jest/globals';
import { signInAs } from '../../testing/auth.fixtures';
import { SessionStore } from '../auth/session.store';
import { provideUrbanIcons } from '../theme/icons';
import { ShellLayout } from './shell.layout';

async function setup(): Promise<{
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
  await signInAs(store, http);
  const fixture = TestBed.createComponent(ShellLayout);
  await fixture.whenStable();
  return { fixture, store, http, router: TestBed.inject(Router), html: fixture.nativeElement };
}

describe('ShellLayout', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('mostra quem esta logado e o papel', async () => {
    const { html } = await setup();

    expect(html.querySelector('[data-testid="current-user"]')?.textContent).toContain(
      'sindica@example.com',
    );
    expect(html.querySelector('[data-testid="current-user"]')?.textContent).toContain('Síndico');
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
});
