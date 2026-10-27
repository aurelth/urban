import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter } from '@angular/router';
import { flushPromises } from '../../../testing/async';
import { problem } from '../../../testing/auth.fixtures';
import { provideUrbanIcons } from '../../../core/theme/icons';
import { ConfirmEmailPage } from './confirm-email.page';

interface Harness {
  fixture: ComponentFixture<ConfirmEmailPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

const PASSWORD = 'Senha-segura-123';

function setup(token?: string): Harness {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
    ],
  });
  const fixture = TestBed.createComponent(ConfirmEmailPage);
  if (token !== undefined) {
    fixture.componentRef.setInput('token', token);
  }
  return { fixture, http: TestBed.inject(HttpTestingController), html: fixture.nativeElement };
}

async function settle(fixture: ComponentFixture<ConfirmEmailPage>): Promise<void> {
  await flushPromises();
  await fixture.whenStable();
  fixture.detectChanges();
}

async function open(version = '2026-10-26', token = 'token-1'): Promise<Harness> {
  const harness = setup(token);
  await harness.fixture.whenStable();
  harness.http.expectOne('/api/terms/current').flush({ version });
  await settle(harness.fixture);
  return harness;
}

function typePassword(html: HTMLElement, value: string): void {
  const input = html.querySelector<HTMLInputElement>('input[autocomplete="new-password"]')!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

function acceptTerms(html: HTMLElement): void {
  html.querySelector<HTMLInputElement>('mat-checkbox input')!.click();
}

async function submit(fixture: ComponentFixture<ConfirmEmailPage>): Promise<void> {
  fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
  await fixture.whenStable();
}

function confirmRequest(http: HttpTestingController): TestRequest {
  return http.expectOne('/api/auth/confirm-email');
}

function rejected(status = 400): { status: number; statusText: string } {
  return { status, statusText: 'Bad Request' };
}

async function sendValid(harness: Harness): Promise<void> {
  typePassword(harness.html, PASSWORD);
  acceptTerms(harness.html);
  await submit(harness.fixture);
}

describe('ConfirmEmailPage', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('sem token, explica que o link esta incompleto e oferece novo cadastro', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();

    http.expectNone('/api/terms/current');
    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Link incompleto');
    expect(html.querySelector('form')).toBeNull();
    expect(html.querySelector('a[href="/cadastrar"]')?.textContent).toContain('novo cadastro');
  });

  it('com token, mostra o formulario e busca a versao vigente dos termos', async () => {
    const { html } = await open();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Criar senha');
    expect(html.querySelector('input[autocomplete="new-password"]')?.getAttribute('type')).toBe(
      'password',
    );
    expect(html.querySelector('a[href="/termos"]')?.getAttribute('target')).toBe('_blank');
    expect(html.querySelector('a[href="/privacidade"]')?.getAttribute('target')).toBe('_blank');
  });

  it('nao chama a API e foca a senha quando nada foi preenchido', async () => {
    const { fixture, http, html } = await open();

    await submit(fixture);
    fixture.detectChanges();

    http.expectNone('/api/auth/confirm-email');
    expect(html.textContent).toContain('Informe a senha');
    expect(html.textContent).toContain('Aceite os termos para continuar');
    expect(document.activeElement).toBe(html.querySelector('input[autocomplete="new-password"]'));
  });

  it.each([
    ['abc', 'A senha precisa ter pelo menos 10 caracteres'],
    ['abcdefghijkl', 'A senha precisa ter pelo menos uma letra e um número'],
    ['a1'.repeat(65), 'A senha pode ter no máximo 128 caracteres'],
  ])('recusa a senha "%s" antes de chamar a API', async (password, message) => {
    const { fixture, http, html } = await open();
    typePassword(html, password);
    acceptTerms(html);

    await submit(fixture);
    fixture.detectChanges();

    http.expectNone('/api/auth/confirm-email');
    expect(html.textContent).toContain(message);
  });

  it('exige aceitar os termos mesmo com a senha valida', async () => {
    const { fixture, http, html } = await open();
    typePassword(html, PASSWORD);

    await submit(fixture);
    fixture.detectChanges();

    http.expectNone('/api/auth/confirm-email');
    expect(html.querySelector('[role="alert"]')?.textContent).toContain(
      'Aceite os termos para continuar',
    );
    expect(document.activeElement).toBe(html.querySelector('mat-checkbox input'));
  });

  it('envia o token, a senha e a versao dos termos e mostra a conta ativada', async () => {
    const harness = await open('2026-10-26', 'token-do-link');

    await sendValid(harness);
    const request = confirmRequest(harness.http);
    expect(request.request.body).toEqual({
      token: 'token-do-link',
      password: PASSWORD,
      termsVersion: '2026-10-26',
    });
    request.flush(null, { status: 204, statusText: 'No Content' });
    await settle(harness.fixture);

    const { html } = harness;
    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Conta ativada');
    expect(html.querySelector('a[href="/entrar"]')?.textContent).toContain('Entrar');
    expect(html.querySelector('form')).toBeNull();
  });

  it.each([
    ['identity.email_verification.invalid', 'Link inválido ou já usado. Faça o cadastro novamente'],
    [
      'identity.email_verification.expired',
      'Link expirado. Faça o cadastro novamente para receber outro',
    ],
  ])('troca o formulario por novo cadastro quando a API diz %s', async (code, message) => {
    const harness = await open();
    await sendValid(harness);

    confirmRequest(harness.http).flush(problem(400, code), rejected());
    await settle(harness.fixture);

    const { html } = harness;
    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Link inválido');
    expect(html.querySelector('[role="alert"]')?.textContent).toContain(message);
    expect(html.querySelector('form')).toBeNull();
    expect(html.querySelector('a[href="/cadastrar"]')?.textContent).toContain('novo cadastro');
  });

  it.each([
    ['identity.password.too_short', 'A senha precisa ter pelo menos 10 caracteres'],
    ['identity.password.too_long', 'A senha pode ter no máximo 128 caracteres'],
    [
      'identity.password.needs_letter_and_digit',
      'A senha precisa ter pelo menos uma letra e um número',
    ],
    [
      'identity.consent.required',
      'Aceite os termos de uso e a política de privacidade para continuar',
    ],
  ])('mantem o formulario e mostra o motivo quando a API diz %s', async (code, message) => {
    const harness = await open();
    await sendValid(harness);

    confirmRequest(harness.http).flush(problem(400, code), rejected());
    await settle(harness.fixture);

    const { html } = harness;
    expect(html.querySelector('[role="alert"]')?.textContent).toContain(message);
    expect(html.querySelector('form')).not.toBeNull();
  });

  it('quando os termos mudaram, busca a nova versao e pede um novo aceite', async () => {
    const harness = await open('2026-10-26');
    await sendValid(harness);

    confirmRequest(harness.http).flush(problem(400, 'identity.consent.outdated'), rejected());
    await flushPromises();
    harness.http.expectOne('/api/terms/current').flush({ version: '2026-12-01' });
    await settle(harness.fixture);

    const { html, fixture, http } = harness;
    expect(html.querySelector('[role="alert"]')?.textContent).toContain(
      'Os termos foram atualizados',
    );
    expect(html.querySelector<HTMLInputElement>('mat-checkbox input')?.checked).toBe(false);

    acceptTerms(html);
    await submit(fixture);
    expect(confirmRequest(http).request.body).toEqual({
      token: 'token-1',
      password: PASSWORD,
      termsVersion: '2026-12-01',
    });
  });

  it('avisa e deixa tentar de novo quando a versao dos termos nao carrega', async () => {
    const { fixture, http, html } = setup('token-1');
    await fixture.whenStable();
    http.expectOne('/api/terms/current').flush(null, { status: 500, statusText: 'Server Error' });
    await settle(fixture);

    expect(html.querySelector('[role="alert"]')?.textContent).toContain(
      'Não foi possível carregar os termos',
    );
    expect(html.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(true);

    const retry = [...html.querySelectorAll('button')].find((b) =>
      b.textContent?.includes('Tentar de novo'),
    )!;
    retry.click();
    await flushPromises();
    http.expectOne('/api/terms/current').flush({ version: '2026-10-26' });
    await settle(fixture);

    expect(html.querySelector<HTMLButtonElement>('button[type="submit"]')?.disabled).toBe(false);
    expect(html.textContent).not.toContain('Não foi possível carregar os termos');
  });

  it('nao envia enquanto a versao dos termos nao chegou', async () => {
    const { fixture, http, html } = setup('token-1');
    await fixture.whenStable();
    typePassword(html, PASSWORD);
    acceptTerms(html);

    await submit(fixture);

    http.expectNone('/api/auth/confirm-email');
    http.expectOne('/api/terms/current');
  });

  it('desabilita o botao enquanto envia e libera depois da resposta', async () => {
    const harness = await open();
    const { html, fixture, http } = harness;
    const button = html.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(button.disabled).toBe(false);
    expect(button.textContent?.trim()).toBe('Ativar conta');

    await sendValid(harness);
    fixture.detectChanges();
    expect(button.disabled).toBe(true);
    expect(button.textContent?.trim()).toBe('Ativando');

    confirmRequest(http).flush(problem(400, 'identity.password.too_short'), rejected());
    await settle(fixture);

    expect(button.disabled).toBe(false);
    expect(button.textContent?.trim()).toBe('Ativar conta');
  });

  it('limpa o aviso de erro quando a pessoa tenta de novo', async () => {
    const harness = await open();
    await sendValid(harness);
    confirmRequest(harness.http).flush(problem(400, 'identity.password.too_short'), rejected());
    await settle(harness.fixture);
    expect(harness.html.querySelector('[role="alert"]')).not.toBeNull();

    await submit(harness.fixture);
    harness.fixture.detectChanges();

    expect(harness.html.querySelector('[role="alert"]')).toBeNull();
    confirmRequest(harness.http);
  });

  it('pede para aguardar quando o limite de pedidos estoura', async () => {
    const harness = await open();
    await sendValid(harness);

    confirmRequest(harness.http).flush(null, {
      status: 429,
      statusText: 'Too Many Requests',
      headers: { 'Retry-After': '45' },
    });
    await settle(harness.fixture);

    expect(harness.html.querySelector('[role="alert"]')?.textContent).toContain(
      'Aguarde 45 segundos',
    );
  });

  it('mostra e oculta a senha', async () => {
    const { fixture, html } = await open();
    const toggle = html.querySelector<HTMLButtonElement>('button[aria-label="Mostrar senha"]')!;
    const password = html.querySelector<HTMLInputElement>('input[autocomplete="new-password"]')!;
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
