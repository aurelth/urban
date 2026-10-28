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
});
