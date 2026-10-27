import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { jest } from '@jest/globals';
import { flushPromises } from '../../../testing/async';
import { MANAGER, problem, tokenResponse } from '../../../testing/auth.fixtures';
import { provideUrbanIcons } from '../../../core/theme/icons';
import { LoginPage } from './login.page';

interface Harness {
  fixture: ComponentFixture<LoginPage>;
  http: HttpTestingController;
  navigate: jest.SpiedFunction<Router['navigateByUrl']>;
  html: HTMLElement;
}

function setup(returnUrl?: string): Harness {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
    ],
  });
  const navigate = jest.spyOn(TestBed.inject(Router), 'navigateByUrl').mockResolvedValue(true);
  const fixture = TestBed.createComponent(LoginPage);
  if (returnUrl !== undefined) {
    fixture.componentRef.setInput('returnUrl', returnUrl);
  }
  return {
    fixture,
    http: TestBed.inject(HttpTestingController),
    navigate,
    html: fixture.nativeElement,
  };
}

function type(html: HTMLElement, selector: string, value: string): void {
  const input = html.querySelector<HTMLInputElement>(selector)!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

async function submit(fixture: ComponentFixture<LoginPage>): Promise<void> {
  fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
  await fixture.whenStable();
}

/** Espera as promessas do envio terminarem e atualiza a tela (o componente é OnPush). */
async function settle(fixture: ComponentFixture<LoginPage>): Promise<void> {
  await flushPromises();
  await fixture.whenStable();
  fixture.detectChanges();
}

function fill(
  html: HTMLElement,
  email = 'sindica@example.com',
  password = 'Senha-segura-123',
): void {
  type(html, 'input[type="email"]', email);
  type(html, 'input[autocomplete="current-password"]', password);
}

function loginRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((r) => r.url === '/api/auth/login');
}

describe('LoginPage', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('mostra o titulo e os campos', async () => {
    const { fixture, html } = setup();
    await fixture.whenStable();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Entrar');
    expect(html.querySelector('input[type="email"]')?.getAttribute('autocomplete')).toBe(
      'username',
    );
    expect(html.querySelector('input[autocomplete="current-password"]')?.getAttribute('type')).toBe(
      'password',
    );
  });

  it('nao chama a API e mostra o que falta quando o formulario esta vazio', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();

    await submit(fixture);
    fixture.detectChanges();

    http.expectNone('/api/auth/login');
    expect(html.textContent).toContain('Informe o e-mail');
    expect(html.textContent).toContain('Informe a senha');
    expect(document.activeElement).toBe(html.querySelector('input[type="email"]'));
  });

  it('pede um e-mail valido', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    fill(html, 'isto-nao-e-email', 'Senha-segura-123');

    await submit(fixture);
    fixture.detectChanges();

    http.expectNone('/api/auth/login');
    expect(html.textContent).toContain('Informe um e-mail válido');
  });

  it('entra e vai para o painel quando nao ha destino', async () => {
    const { fixture, http, navigate, html } = setup();
    await fixture.whenStable();
    fill(html);

    await submit(fixture);
    const login = loginRequest(http);
    expect(login.request.body).toEqual({
      email: 'sindica@example.com',
      password: 'Senha-segura-123',
    });
    login.flush(tokenResponse());
    await flushPromises();
    http.expectOne('/api/auth/me').flush(MANAGER);
    await settle(fixture);

    expect(navigate).toHaveBeenCalledWith('/dashboard');
  });

  it('volta para o destino guardado quando ele e interno', async () => {
    const { fixture, http, navigate, html } = setup('/moradores?pagina=2');
    await fixture.whenStable();
    fill(html);

    await submit(fixture);
    loginRequest(http).flush(tokenResponse());
    await flushPromises();
    http.expectOne('/api/auth/me').flush(MANAGER);
    await settle(fixture);

    expect(navigate).toHaveBeenCalledWith('/moradores?pagina=2');
  });

  it('ignora um destino que sai do site', async () => {
    const { fixture, http, navigate, html } = setup('https://outro.example');
    await fixture.whenStable();
    fill(html);

    await submit(fixture);
    loginRequest(http).flush(tokenResponse());
    await flushPromises();
    http.expectOne('/api/auth/me').flush(MANAGER);
    await settle(fixture);

    expect(navigate).toHaveBeenCalledWith('/dashboard');
  });

  it.each([
    [401, 'identity.login.invalid_credentials', 'E-mail ou senha incorretos'],
    [
      423,
      'identity.login.locked_out',
      'Conta bloqueada por muitas tentativas. Tente novamente em 15 minutos',
    ],
    [
      403,
      'identity.user.email_not_verified',
      'Confirme seu e-mail pelo link que enviamos antes de entrar',
    ],
    [403, 'identity.user.account_deactivated', 'Conta desativada. Fale com o síndico'],
    [403, 'identity.user.account_suspended', 'Acesso suspenso. Fale com o suporte'],
    [403, 'identity.user.account_deleted', 'Conta excluída'],
  ])('mostra o motivo quando a API responde %i %s', async (status, code, message) => {
    const { fixture, http, navigate, html } = setup();
    await fixture.whenStable();
    fill(html);

    await submit(fixture);
    loginRequest(http).flush(problem(status, code), { status, statusText: 'Erro' });
    await settle(fixture);
    fixture.detectChanges();

    expect(html.querySelector('[role="alert"]')?.textContent).toContain(message);
    expect(navigate).not.toHaveBeenCalled();
  });

  it('pede para aguardar quando o limite de tentativas estoura', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    fill(html);

    await submit(fixture);
    loginRequest(http).flush(null, {
      status: 429,
      statusText: 'Too Many Requests',
      headers: { 'Retry-After': '20' },
    });
    await settle(fixture);
    fixture.detectChanges();

    expect(html.querySelector('[role="alert"]')?.textContent).toContain('Aguarde 20 segundos');
  });

  it('desabilita o botao enquanto envia e libera depois da resposta', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    fill(html);
    const button = html.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(button.disabled).toBe(false);
    expect(button.textContent?.trim()).toBe('Entrar');

    await submit(fixture);
    fixture.detectChanges();
    expect(button.disabled).toBe(true);
    expect(button.textContent?.trim()).toBe('Entrando');

    loginRequest(http).flush(problem(401, 'identity.login.invalid_credentials'), {
      status: 401,
      statusText: 'Unauthorized',
    });
    await settle(fixture);
    fixture.detectChanges();

    expect(button.disabled).toBe(false);
    expect(button.textContent?.trim()).toBe('Entrar');
  });

  it('limpa o aviso de erro quando a pessoa tenta de novo', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    fill(html);
    await submit(fixture);
    loginRequest(http).flush(problem(401, 'identity.login.invalid_credentials'), {
      status: 401,
      statusText: 'Unauthorized',
    });
    await settle(fixture);
    fixture.detectChanges();
    expect(html.querySelector('[role="alert"]')).not.toBeNull();

    await submit(fixture);
    fixture.detectChanges();

    expect(html.querySelector('[role="alert"]')).toBeNull();
    loginRequest(http);
  });

  it('mostra e oculta a senha', async () => {
    const { fixture, html } = setup();
    await fixture.whenStable();
    const toggle = html.querySelector<HTMLButtonElement>('button[aria-label="Mostrar senha"]')!;
    const password = html.querySelector<HTMLInputElement>(
      'input[autocomplete="current-password"]',
    )!;
    expect(toggle.getAttribute('aria-pressed')).toBe('false');

    toggle.click();
    fixture.detectChanges();
    expect(password.type).toBe('text');
    expect(toggle.getAttribute('aria-label')).toBe('Ocultar senha');
    expect(toggle.getAttribute('aria-pressed')).toBe('true');

    toggle.click();
    fixture.detectChanges();
    expect(password.type).toBe('password');
  });
});
