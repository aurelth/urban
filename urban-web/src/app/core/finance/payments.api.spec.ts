import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PaymentsApi } from './payments.api';
import { DEFAULT_PAYMENTS_QUERY } from './payments.models';

describe('PaymentsApi', () => {
  let api: PaymentsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(PaymentsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lista as cobrancas do mes sem filtro de situacao quando a situacao e "todas"', () => {
    api.listMonth('2026-11', DEFAULT_PAYMENTS_QUERY).subscribe();

    const request = http.expectOne((r) => r.url === '/api/finance/months/2026-11/payments');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.has('status')).toBe(false);
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('50');
    request.flush({});
  });

  it('manda a situacao e a pagina pedidas', () => {
    api.listMonth('2026-11', { status: 'overdue', page: 3, pageSize: 20 }).subscribe();

    const request = http.expectOne((r) => r.url === '/api/finance/months/2026-11/payments');
    expect(request.request.params.get('status')).toBe('overdue');
    expect(request.request.params.get('page')).toBe('3');
    expect(request.request.params.get('pageSize')).toBe('20');
    request.flush({});
  });

  it('le os pagamentos de uma cobranca', () => {
    api.listCharge('c1').subscribe();

    const request = http.expectOne('/api/finance/charges/c1/payments');
    expect(request.request.method).toBe('GET');
    request.flush({});
  });

  it('registra um pagamento', () => {
    const payload = { amount: 500, receivedOn: '2026-11-10', method: 'pix', note: null } as const;

    api.register('c1', payload).subscribe();

    const request = http.expectOne('/api/finance/charges/c1/payments');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(payload);
    request.flush({});
  });

  it('estorna um pagamento com o motivo', () => {
    api.reverse('p1', 'Valor lançado na unidade errada').subscribe();

    const request = http.expectOne('/api/finance/payments/p1/reverse');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ reason: 'Valor lançado na unidade errada' });
    request.flush(null);
  });
});
