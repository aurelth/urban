import { TestBed } from '@angular/core/testing';
import { HttpTestingController } from '@angular/common/http/testing';
import {
  accepted,
  setupComponent,
  settle,
  submitForm,
  typeEmail,
} from '../../../testing/component.harness';
import { RegisterPage } from './register.page';

describe('RegisterPage', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).match(() => true);
  });

  it('mostra o titulo e o caminho para quem ja tem conta', async () => {
    const { fixture, html } = setupComponent(RegisterPage);
    await fixture.whenStable();

    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Criar conta');
    expect(html.textContent).toContain('Cadastro de síndicos');
    expect(html.querySelector('a[href="/entrar"]')?.textContent?.trim()).toBe('Entrar');
  });

  it('pede o link do cadastro so com o e-mail e avisa que ele vale por 24 horas', async () => {
    const { fixture, http, html } = setupComponent(RegisterPage);
    await fixture.whenStable();
    typeEmail(html, 'sindica@example.com');

    await submitForm(fixture);
    const request = http.expectOne('/api/auth/register');
    expect(request.request.body).toEqual({ email: 'sindica@example.com' });
    request.flush(null, accepted());
    await settle(fixture);

    const notice = html.querySelector('[role="status"]')?.textContent;
    expect(notice).toContain('sindica@example.com');
    expect(notice).toContain('confirmar o cadastro e criar sua senha');
    expect(notice).toContain('O link vale por 24 horas');
  });
});
