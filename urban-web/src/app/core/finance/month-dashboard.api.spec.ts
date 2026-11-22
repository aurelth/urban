import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MonthDashboardApi } from './month-dashboard.api';

describe('MonthDashboardApi', () => {
  let api: MonthDashboardApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(MonthDashboardApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('lê o painel do mês pedido', () => {
    let received: unknown;
    api.get('2026-11').subscribe((data) => (received = data));

    const request = http.expectOne('/api/finance/months/2026-11/dashboard');
    expect(request.request.method).toBe('GET');
    request.flush({ month: '2026-11' });

    expect(received).toEqual({ month: '2026-11' });
  });

  it('não manda parâmetros: o mês vai na rota', () => {
    api.get('2026-10').subscribe();

    const request = http.expectOne('/api/finance/months/2026-10/dashboard');
    expect(request.request.params.keys()).toEqual([]);
    request.flush({});
  });
});
