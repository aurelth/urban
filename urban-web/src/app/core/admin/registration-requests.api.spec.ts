import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  REGISTRATION_REQUESTS_URL,
  registrationRequest,
} from '../../testing/registration-requests.fixtures';
import { RegistrationRequestsApi } from './registration-requests.api';

function setup(): { api: RegistrationRequestsApi; http: HttpTestingController } {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return {
    api: TestBed.inject(RegistrationRequestsApi),
    http: TestBed.inject(HttpTestingController),
  };
}

describe('RegistrationRequestsApi', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('lista os pedidos que aguardam decisao', () => {
    const { api, http } = setup();
    const received: unknown[] = [];

    api.list().subscribe((items) => received.push(items));

    const request = http.expectOne(REGISTRATION_REQUESTS_URL);
    expect(request.request.method).toBe('GET');
    request.flush([registrationRequest()]);
    expect(received).toEqual([[registrationRequest()]]);
  });

  it('aprova um pedido pelo id', () => {
    const { api, http } = setup();

    api.approve('abc-1').subscribe();

    const request = http.expectOne(`${REGISTRATION_REQUESTS_URL}/abc-1/approve`);
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });
  });

  it('recusa um pedido pelo id', () => {
    const { api, http } = setup();

    api.reject('abc-1').subscribe();

    const request = http.expectOne(`${REGISTRATION_REQUESTS_URL}/abc-1/reject`);
    expect(request.request.method).toBe('POST');
    request.flush(null, { status: 204, statusText: 'No Content' });
  });
});
