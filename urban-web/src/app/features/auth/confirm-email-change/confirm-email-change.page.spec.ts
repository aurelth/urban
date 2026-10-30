import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SessionStore } from '../../../core/auth/session.store';
import { REGISTERED_MANAGER, problem, signInAs } from '../../../testing/auth.fixtures';
import { Harness, setupComponent, settle } from '../../../testing/component.harness';
import { ConfirmEmailChangePage } from './confirm-email-change.page';

const ROUTE = '/api/auth/confirm-email-change';

function open(token?: string): Harness<ConfirmEmailChangePage> {
  const harness = setupComponent(ConfirmEmailChangePage);
  if (token !== undefined) {
    harness.fixture.componentRef.setInput('token', token);
  }
  return harness;
}

function rejected(
  status: number,
  code: string,
): [ReturnType<typeof problem>, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

describe('ConfirmEmailChangePage', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('sem token, explica que o link esta incompleto, sem chamar a API', async () => {
    const { fixture, html, http } = open();
    fixture.detectChanges();
    await fixture.whenStable();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Link incompleto');
    expect(html.querySelector('a[href="/entrar"]')).not.toBeNull();
    http.expectNone(ROUTE);
  });

  it('com token, confirma sozinho ao abrir, uma vez so, mandando o token', async () => {
    const { fixture, html, http } = open('segredo-do-link');
    fixture.detectChanges();

    const request = http.expectOne(ROUTE);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ token: 'segredo-do-link' });
    expect(html.querySelector('[role="status"]')?.textContent).toContain(
      'Confirmando o novo e-mail',
    );
    request.flush(null, { status: 204, statusText: 'No Content' });
    await settle(fixture);
    http.expectNone(ROUTE);
  });

  it('com sucesso avisa em verde e leva a entrada com o e-mail novo', async () => {
    const { fixture, html, http } = open('segredo-do-link');
    fixture.detectChanges();

    http.expectOne(ROUTE).flush(null, { status: 204, statusText: 'No Content' });
    await settle(fixture);

    const status = html.querySelector('[role="status"]');
    expect(status?.querySelector('h1')?.textContent?.trim()).toBe('E-mail alterado');
    expect(status?.querySelector('.text-success mat-icon')).not.toBeNull();
    expect(status?.textContent).toContain('Entre com o novo e-mail');
    expect(html.querySelector('a[href="/entrar"]')?.textContent?.trim()).toBe('Entrar');
  });

  it('com sucesso encerra a sessao deste navegador, porque a API encerrou todas', async () => {
    const { fixture, http } = open('segredo-do-link');
    const store = TestBed.inject(SessionStore);
    await signInAs(store, http, REGISTERED_MANAGER);
    fixture.detectChanges();

    http.expectOne(ROUTE).flush(null, { status: 204, statusText: 'No Content' });
    await settle(fixture);

    expect(store.status()).toBe('anonymous');
    expect(store.expired()).toBe(false);
    http.expectNone('/api/auth/logout');
  });

  it.each([
    ['identity.email_change.invalid', 'Link inválido ou já usado. Peça a troca de e-mail de novo'],
    ['identity.email_change.expired', 'Link expirado. Peça a troca de e-mail de novo'],
  ])('para %s, mostra o aviso de link e oferece voltar a entrada', async (code, message) => {
    const { fixture, html, http } = open('segredo-do-link');
    fixture.detectChanges();
    const [body, options] = rejected(400, code);

    http.expectOne(ROUTE).flush(body, options);
    await settle(fixture);

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Link inválido');
    expect(html.querySelector('[role="alert"]')?.textContent?.trim()).toBe(message);
    expect(html.querySelector('a[href="/entrar"]')?.textContent).toContain('Ir para a entrada');
  });

  it('um link recusado nao encerra a sessao de quem esta logado', async () => {
    const { fixture, http } = open('segredo-do-link');
    const store = TestBed.inject(SessionStore);
    await signInAs(store, http, REGISTERED_MANAGER);
    fixture.detectChanges();
    const [body, options] = rejected(400, 'identity.email_change.invalid');

    http.expectOne(ROUTE).flush(body, options);
    await settle(fixture);

    expect(store.status()).toBe('authenticated');
  });

  it('se a rede falhar, avisa e deixa tentar de novo com o mesmo link', async () => {
    const { fixture, html, http } = open('segredo-do-link');
    fixture.detectChanges();

    http.expectOne(ROUTE).flush(null, { status: 503, statusText: 'Service Unavailable' });
    await settle(fixture);
    expect(html.querySelector('[role="alert"]')?.textContent).toContain(
      'Serviço temporariamente indisponível',
    );

    html.querySelector<HTMLButtonElement>('[data-testid="retry"]')!.click();
    const retry = http.expectOne(ROUTE);
    expect(retry.request.body).toEqual({ token: 'segredo-do-link' });
    retry.flush(null, { status: 204, statusText: 'No Content' });
    await settle(fixture);

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('E-mail alterado');
  });
});
