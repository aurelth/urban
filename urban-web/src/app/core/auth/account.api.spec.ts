import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { AccountApi } from './account.api';

describe('AccountApi', () => {
  let api: AccountApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(AccountApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('troca a senha com POST /api/auth/change-password, mandando a atual e a nova', () => {
    let done = false;

    api.changePassword('Senha-antiga-123', 'Senha-nova-456').subscribe(() => (done = true));
    const request = http.expectOne('/api/auth/change-password');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      currentPassword: 'Senha-antiga-123',
      newPassword: 'Senha-nova-456',
    });
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(done).toBe(true);
  });

  it('pede a troca de e-mail com POST /api/auth/change-email, mandando a senha e o novo endereco', () => {
    let done = false;

    api.requestEmailChange('Senha-segura-123', 'nova@example.com').subscribe(() => (done = true));
    const request = http.expectOne('/api/auth/change-email');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      password: 'Senha-segura-123',
      newEmail: 'nova@example.com',
    });
    request.flush(null, { status: 202, statusText: 'Accepted' });

    expect(done).toBe(true);
  });

  it('confirma a troca de e-mail com POST /api/auth/confirm-email-change, mandando so o token', () => {
    let done = false;

    api.confirmEmailChange('segredo-do-link').subscribe(() => (done = true));
    const request = http.expectOne('/api/auth/confirm-email-change');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ token: 'segredo-do-link' });
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(done).toBe(true);
  });
});
