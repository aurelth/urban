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
import { ForgotPasswordPage } from './forgot-password.page';

interface Harness {
  fixture: ComponentFixture<ForgotPasswordPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

function setup(): Harness {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
    ],
  });
  const fixture = TestBed.createComponent(ForgotPasswordPage);
  return { fixture, http: TestBed.inject(HttpTestingController), html: fixture.nativeElement };
}

function typeEmail(html: HTMLElement, value: string): void {
  const input = html.querySelector<HTMLInputElement>('input[type="email"]')!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

async function submit(fixture: ComponentFixture<ForgotPasswordPage>): Promise<void> {
  fixture.nativeElement.querySelector('form').dispatchEvent(new Event('submit'));
  await fixture.whenStable();
}

async function settle(fixture: ComponentFixture<ForgotPasswordPage>): Promise<void> {
  await flushPromises();
  await fixture.whenStable();
  fixture.detectChanges();
}

function forgotRequest(http: HttpTestingController): TestRequest {
  return http.expectOne('/api/auth/forgot-password');
}

function accepted(): { status: number; statusText: string } {
  return { status: 202, statusText: 'Accepted' };
}

describe('ForgotPasswordPage', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('mostra o título, o campo de e-mail e o caminho de volta para a entrada', async () => {
    const { fixture, html } = setup();
    await fixture.whenStable();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Esqueci a senha');
    expect(html.querySelector('input[type="email"]')?.getAttribute('autocomplete')).toBe('email');
    expect(html.querySelector('a[href="/entrar"]')?.textContent?.trim()).toBe('Entrar');
  });

  it('não chama a API e foca o campo quando o e-mail está vazio', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();

    await submit(fixture);
    fixture.detectChanges();

    http.expectNone('/api/auth/forgot-password');
    expect(html.textContent).toContain('Informe o e-mail');
    expect(document.activeElement).toBe(html.querySelector('input[type="email"]'));
  });

  it('pede um e-mail válido', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    typeEmail(html, 'isto-nao-e-email');

    await submit(fixture);
    fixture.detectChanges();

    http.expectNone('/api/auth/forgot-password');
    expect(html.textContent).toContain('Informe um e-mail válido');
  });

  it('envia só o e-mail e avisa para verificar a caixa de entrada, sem confirmar se a conta existe', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    typeEmail(html, 'sindica@example.com');

    await submit(fixture);
    const request = forgotRequest(http);
    expect(request.request.body).toEqual({ email: 'sindica@example.com' });
    request.flush(null, accepted());
    await settle(fixture);

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Verifique seu e-mail');
    const notice = html.querySelector('[role="status"]')?.textContent;
    expect(notice).toContain('sindica@example.com');
    expect(notice).toContain('Se o endereço');
    expect(notice).toContain('estiver cadastrado');
    expect(notice).toContain('O link vale por 1 hora');
    expect(notice).toContain('caixa de spam');
    expect(html.querySelector('form')).toBeNull();
    expect(html.querySelector('a[href="/entrar"]')?.textContent).toContain('Voltar para a entrada');
  });

  it('volta ao formulário, com o e-mail preenchido, ao escolher outro e-mail', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    typeEmail(html, 'sindica@example.com');
    await submit(fixture);
    forgotRequest(http).flush(null, accepted());
    await settle(fixture);

    html.querySelector<HTMLButtonElement>('button[type="button"]')!.click();
    await settle(fixture);

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Esqueci a senha');
    expect(html.querySelector<HTMLInputElement>('input[type="email"]')?.value).toBe(
      'sindica@example.com',
    );
  });

  it('desabilita o botão enquanto envia e libera depois da resposta', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    typeEmail(html, 'sindica@example.com');
    const button = html.querySelector<HTMLButtonElement>('button[type="submit"]')!;
    expect(button.disabled).toBe(false);
    expect(button.textContent?.trim()).toBe('Enviar link');

    await submit(fixture);
    fixture.detectChanges();
    expect(button.disabled).toBe(true);
    expect(button.textContent?.trim()).toBe('Enviando');

    forgotRequest(http).flush(problem(400, 'identity.email.invalid'), {
      status: 400,
      statusText: 'Bad Request',
    });
    await settle(fixture);

    expect(button.disabled).toBe(false);
    expect(button.textContent?.trim()).toBe('Enviar link');
  });

  it('mostra o motivo quando a API recusa o e-mail', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    typeEmail(html, 'sindica@example.com');

    await submit(fixture);
    forgotRequest(http).flush(problem(400, 'identity.email.invalid'), {
      status: 400,
      statusText: 'Bad Request',
    });
    await settle(fixture);

    expect(html.querySelector('[role="alert"]')?.textContent).toContain('Informe um e-mail válido');
    expect(html.querySelector('form')).not.toBeNull();
  });

  it('pede para aguardar quando o limite de pedidos estoura', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    typeEmail(html, 'sindica@example.com');

    await submit(fixture);
    forgotRequest(http).flush(null, {
      status: 429,
      statusText: 'Too Many Requests',
      headers: { 'Retry-After': '30' },
    });
    await settle(fixture);

    expect(html.querySelector('[role="alert"]')?.textContent).toContain('Aguarde 30 segundos');
  });

  it('limpa o aviso de erro quando a pessoa tenta de novo', async () => {
    const { fixture, http, html } = setup();
    await fixture.whenStable();
    typeEmail(html, 'sindica@example.com');
    await submit(fixture);
    forgotRequest(http).flush(null, { status: 503, statusText: 'Service Unavailable' });
    await settle(fixture);
    expect(html.querySelector('[role="alert"]')).not.toBeNull();

    await submit(fixture);
    fixture.detectChanges();

    expect(html.querySelector('[role="alert"]')).toBeNull();
    forgotRequest(http);
  });
});
