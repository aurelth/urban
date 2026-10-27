import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { RegistrationApi } from './registration.api';

function setup() {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { api: TestBed.inject(RegistrationApi), http: TestBed.inject(HttpTestingController) };
}

describe('RegistrationApi', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController).verify();
  });

  it('cadastra só com o e-mail', () => {
    const { api, http } = setup();

    api.register('sindica@example.com').subscribe();

    const request = http.expectOne('/api/auth/register');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ email: 'sindica@example.com' });
    request.flush(null, { status: 202, statusText: 'Accepted' });
  });

  it('confirma o e-mail com o token, a senha e a versão dos termos', () => {
    const { api, http } = setup();

    api.confirmEmail('token-1', 'Senha-segura-123', '2026-10-26').subscribe();

    const request = http.expectOne('/api/auth/confirm-email');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      token: 'token-1',
      password: 'Senha-segura-123',
      termsVersion: '2026-10-26',
    });
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('lê a versão vigente dos termos', () => {
    const { api, http } = setup();
    let version = '';

    api.currentTerms().subscribe((terms) => (version = terms.version));

    const request = http.expectOne('/api/terms/current');
    expect(request.request.method).toBe('GET');
    request.flush({ version: '2026-10-26' });
    expect(version).toBe('2026-10-26');
  });
});
