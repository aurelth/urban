import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { InvoiceDeliveriesApi } from './invoice-deliveries.api';
import { DEFAULT_INVOICE_DELIVERIES_QUERY } from './invoice-deliveries.models';

describe('InvoiceDeliveriesApi', () => {
  let api: InvoiceDeliveriesApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(InvoiceDeliveriesApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('comeca na primeira pagina, com 50 por pagina', () => {
    expect(DEFAULT_INVOICE_DELIVERIES_QUERY).toEqual({ page: 1, pageSize: 50 });
  });

  it('lista os envios do mes, com a pagina pedida', () => {
    api.listMonth('2026-11', { page: 3, pageSize: 20 }).subscribe();

    const request = http.expectOne(
      (r) => r.url === '/api/finance/months/2026-11/invoice-deliveries',
    );
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('3');
    expect(request.request.params.get('pageSize')).toBe('20');
    request.flush({});
  });

  it('manda as faturas do mes, sem corpo, e devolve o resumo', () => {
    let result: unknown;

    api.sendMonth('2026-11').subscribe((value) => (result = value));

    const request = http.expectOne('/api/finance/months/2026-11/invoice-deliveries');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    const summary = { queued: 2, alreadySent: 1, alreadyQueued: 0, withoutRecipient: 1 };
    request.flush(summary, { status: 202, statusText: 'Accepted' });
    expect(result).toEqual(summary);
  });

  it('reenvia a fatura de uma cobranca, sem corpo', () => {
    api.resend('c1').subscribe();

    const request = http.expectOne('/api/finance/charges/c1/invoice-deliveries');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toBeNull();
    request.flush(null, { status: 202, statusText: 'Accepted' });
  });
});
