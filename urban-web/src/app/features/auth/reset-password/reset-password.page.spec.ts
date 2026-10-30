import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SessionStore } from '../../../core/auth/session.store';
import { problem, signInAs } from '../../../testing/auth.fixtures';
import {
  Harness,
  setupComponent,
  settle,
  submitForm,
  typePassword,
} from '../../../testing/component.harness';
import { ResetPasswordPage } from './reset-password.page';

const NEW_PASSWORD = 'Nova-senha-123';

function open(token?: string): Harness<ResetPasswordPage> {
  const harness = setupComponent(ResetPasswordPage);
  if (token !== undefined) {
    harness.fixture.componentRef.setInput('token', token);
  }
  return harness;
}

function rejected(
  code: string,
): [ReturnType<typeof problem>, { status: number; statusText: string }] {
  return [problem(400, code), { status: 400, statusText: 'Bad Request' }];
}

describe('ResetPasswordPage', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('sem token, explica que o link esta incompleto e oferece pedir outro', async () => {
    const { fixture, html } = open();
    await fixture.whenStable();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Link incompleto');
    expect(html.querySelector('form')).toBeNull();
    expect(html.querySelector('a[href="/esqueci-a-senha"]')?.textContent).toContain('novo link');
  });

  it('com token, mostra o formulario da nova senha com a politica de senha', async () => {
    const { fixture, html } = open('token-1');
    await fixture.whenStable();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Redefinir senha');
    expect(html.querySelector('input[autocomplete="new-password"]')?.getAttribute('type')).toBe(
      'password',
    );
    expect(html.textContent).toContain('Mínimo de 10 caracteres');
  });

  it('nao chama a API e foca a senha quando nada foi preenchido', async () => {
    const { fixture, http, html } = open('token-1');
    await fixture.whenStable();

    await submitForm(fixture);
    fixture.detectChanges();

    http.expectNone('/api/auth/reset-password');
    expect(html.textContent).toContain('Informe a senha');
    expect(document.activeElement).toBe(html.querySelector('input[autocomplete="new-password"]'));
  });

  it.each([
    ['curta1', 'A senha precisa ter pelo menos 10 caracteres'],
    ['somente-letras', 'A senha precisa ter pelo menos uma letra e um número'],
  ])('recusa a senha "%s" antes de chamar a API', async (password, message) => {
    const { fixture, http, html } = open('token-1');
    await fixture.whenStable();
    typePassword(html, password);

    await submitForm(fixture);
    fixture.detectChanges();

    http.expectNone('/api/auth/reset-password');
    expect(html.textContent).toContain(message);
  });

  it('envia o token e a nova senha, avisa que a senha mudou e oferece entrar', async () => {
    const { fixture, http, html } = open('token-do-link');
    await fixture.whenStable();
    typePassword(html, NEW_PASSWORD);

    await submitForm(fixture);
    const request = http.expectOne('/api/auth/reset-password');
    expect(request.request.body).toEqual({ token: 'token-do-link', password: NEW_PASSWORD });
    request.flush(null, { status: 204, statusText: 'No Content' });
    await settle(fixture);

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Senha alterada');
    expect(html.querySelector('h1')?.classList).toContain('text-success');
    expect(html.querySelector('[role="status"] mat-icon')?.parentElement?.classList).toContain(
      'text-success',
    );
    expect(html.querySelector('[role="status"]')?.textContent).toContain('outros acessos');
    expect(html.querySelector('form')).toBeNull();
    expect(html.querySelector('a[href="/entrar"]')?.textContent).toContain('Entrar');
  });

  it('encerra a sessao deste navegador, porque a API revoga todas ao trocar a senha', async () => {
    const { fixture, http, html } = open('token-do-link');
    const session = TestBed.inject(SessionStore);
    await signInAs(session, http);
    expect(session.status()).toBe('authenticated');
    await fixture.whenStable();
    typePassword(html, NEW_PASSWORD);

    await submitForm(fixture);
    http
      .expectOne('/api/auth/reset-password')
      .flush(null, { status: 204, statusText: 'No Content' });
    await settle(fixture);

    expect(session.status()).toBe('anonymous');
  });

  it.each([
    ['identity.password_reset.invalid', 'Link inválido ou já usado'],
    ['identity.password_reset.expired', 'Link expirado'],
  ])('troca o formulario por um novo pedido quando a API responde %s', async (code, message) => {
    const { fixture, http, html } = open('token-velho');
    await fixture.whenStable();
    typePassword(html, NEW_PASSWORD);

    await submitForm(fixture);
    http.expectOne('/api/auth/reset-password').flush(...rejected(code));
    await settle(fixture);

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Link inválido');
    expect(html.querySelector('[role="alert"]')?.textContent).toContain(message);
    expect(html.querySelector('form')).toBeNull();
    expect(html.querySelector('a[href="/esqueci-a-senha"]')?.textContent).toContain('novo link');
  });

  it('mantem o formulario e mostra o motivo quando a API recusa a senha', async () => {
    const { fixture, http, html } = open('token-1');
    await fixture.whenStable();
    typePassword(html, NEW_PASSWORD);

    await submitForm(fixture);
    http.expectOne('/api/auth/reset-password').flush(...rejected('identity.password.too_short'));
    await settle(fixture);

    expect(html.querySelector('[role="alert"]')?.textContent).toContain('pelo menos 10 caracteres');
    expect(html.querySelector('form')).not.toBeNull();
  });

  it('desabilita o botao enquanto envia e pede para aguardar no limite de pedidos', async () => {
    const { fixture, http, html } = open('token-1');
    await fixture.whenStable();
    typePassword(html, NEW_PASSWORD);
    const button = html.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(button.textContent?.trim()).toBe('Salvar senha');

    await submitForm(fixture);
    fixture.detectChanges();
    expect(button.disabled).toBe(true);
    expect(button.textContent?.trim()).toBe('Salvando');

    http.expectOne('/api/auth/reset-password').flush(null, {
      status: 429,
      statusText: 'Too Many Requests',
      headers: { 'Retry-After': '30' },
    });
    await settle(fixture);

    expect(button.disabled).toBe(false);
    expect(html.querySelector('[role="alert"]')?.textContent).toContain('Aguarde 30 segundos');
  });

  it('limpa o aviso de erro quando a pessoa tenta de novo', async () => {
    const { fixture, http, html } = open('token-1');
    await fixture.whenStable();
    typePassword(html, NEW_PASSWORD);
    await submitForm(fixture);
    http
      .expectOne('/api/auth/reset-password')
      .flush(null, { status: 503, statusText: 'Service Unavailable' });
    await settle(fixture);
    expect(html.querySelector('[role="alert"]')).not.toBeNull();

    await submitForm(fixture);
    fixture.detectChanges();

    expect(html.querySelector('[role="alert"]')).toBeNull();
    http.expectOne('/api/auth/reset-password');
  });
});
