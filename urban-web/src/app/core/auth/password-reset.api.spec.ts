import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PasswordResetApi } from './password-reset.api';

function setup() {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { api: TestBed.inject(PasswordResetApi), http: TestBed.inject(HttpTestingController) };
}

describe('PasswordResetApi', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('pede o link de redefinição só com o e-mail', () => {
    const { api, http } = setup();

    api.requestLink('sindica@example.com').subscribe();

    const request = http.expectOne('/api/auth/forgot-password');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'sindica@example.com' });
    request.flush(null, { status: 202, statusText: 'Accepted' });
  });

  it('envia o token do link e a nova senha para redefinir', () => {
    const { api, http } = setup();

    api.reset('token-do-link', 'Senha-segura-123').subscribe();

    const request = http.expectOne('/api/auth/reset-password');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ token: 'token-do-link', password: 'Senha-segura-123' });
    request.flush(null, { status: 204, statusText: 'No Content' });
  });
});
