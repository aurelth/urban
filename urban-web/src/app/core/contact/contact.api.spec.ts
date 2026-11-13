import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { SEND_CONTACT_URL } from '../../testing/contact.fixtures';
import { ContactApi } from './contact.api';

function setup(): { api: ContactApi; http: HttpTestingController } {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { api: TestBed.inject(ContactApi), http: TestBed.inject(HttpTestingController) };
}

describe('ContactApi', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('envia assunto e mensagem sem espaços nas pontas e devolve o recibo', () => {
    const { api, http } = setup();
    const received: unknown[] = [];

    api
      .send('  Corrigir o CNPJ ', '\n Digitei o CNPJ errado.  ')
      .subscribe((r) => received.push(r));

    const request = http.expectOne(SEND_CONTACT_URL);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({
      subject: 'Corrigir o CNPJ',
      body: 'Digitei o CNPJ errado.',
    });
    request.flush(
      { id: 'm-1', sentAt: '2026-11-13T15:05:00+00:00' },
      { status: 201, statusText: 'Created' },
    );
    expect(received).toEqual([{ id: 'm-1', sentAt: '2026-11-13T15:05:00+00:00' }]);
  });
});
