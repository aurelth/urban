import { HttpTestingController } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  accepted,
  setupComponent,
  settle,
  submitForm,
  typeEmail,
} from '../../../testing/component.harness';
import { ForgotPasswordPage } from './forgot-password.page';

describe('ForgotPasswordPage', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('mostra o título e o caminho de volta para a entrada', async () => {
    const { fixture, html } = setupComponent(ForgotPasswordPage);
    await fixture.whenStable();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Esqueci a senha');
    expect(html.textContent).toContain('receber o link de redefinição');
    expect(html.querySelector('a[href="/entrar"]')?.textContent?.trim()).toBe('Entrar');
  });

  it('pede o link só com o e-mail, sem confirmar se a conta existe, e oferece voltar', async () => {
    const { fixture, http, html } = setupComponent(ForgotPasswordPage);
    await fixture.whenStable();
    typeEmail(html, 'sindica@example.com');

    await submitForm(fixture);
    const request = http.expectOne('/api/auth/forgot-password');
    expect(request.request.body).toEqual({ email: 'sindica@example.com' });
    request.flush(null, accepted());
    await settle(fixture);

    const notice = html.querySelector('[role="status"]')?.textContent;
    expect(notice).toContain('Se o endereço');
    expect(notice).toContain('sindica@example.com');
    expect(notice).toContain('estiver cadastrado');
    expect(notice).toContain('O link vale por 1 hora');
    expect(html.querySelector('a[href="/entrar"]')?.textContent).toContain('Voltar para a entrada');
  });
});
