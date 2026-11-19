import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { DelinquencyApi } from './delinquency.api';
import { DEFAULT_DELINQUENCY_QUERY } from './delinquency.models';

describe('DelinquencyApi', () => {
  let api: DelinquencyApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(DelinquencyApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lê a inadimplência na primeira página, com 50 unidades', () => {
    let received: unknown;
    api.list(DEFAULT_DELINQUENCY_QUERY).subscribe((data) => (received = data));

    const request = http.expectOne((r) => r.url === '/api/finance/delinquency');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('page')).toBe('1');
    expect(request.request.params.get('pageSize')).toBe('50');
    request.flush({ totalOverdue: 10 });

    expect(received).toEqual({ totalOverdue: 10 });
  });

  it('manda a página e o tamanho pedidos', () => {
    api.list({ page: 3, pageSize: 20 }).subscribe();

    const request = http.expectOne((r) => r.url === '/api/finance/delinquency');
    expect(request.request.params.get('page')).toBe('3');
    expect(request.request.params.get('pageSize')).toBe('20');
    request.flush({});
  });
});
