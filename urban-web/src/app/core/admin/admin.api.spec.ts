import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  MANAGERS_URL,
  PLATFORM_SUMMARY_URL,
  manager,
  managersPage,
  platformSummary,
} from '../../testing/admin.fixtures';
import { AdminApi } from './admin.api';
import { DEFAULT_MANAGERS_QUERY } from './admin.models';

function setup(): { api: AdminApi; http: HttpTestingController } {
  TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
  return { api: TestBed.inject(AdminApi), http: TestBed.inject(HttpTestingController) };
}

const NO_CONTENT = { status: 204, statusText: 'No Content' };

describe('AdminApi', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('busca o resumo da plataforma', () => {
    const { api, http } = setup();
    const received: unknown[] = [];

    api.summary().subscribe((summary) => received.push(summary));

    const request = http.expectOne(PLATFORM_SUMMARY_URL);
    expect(request.request.method).toBe('GET');
    request.flush(platformSummary());
    expect(received).toEqual([platformSummary()]);
  });

  describe('listManagers', () => {
    it('sem filtros, manda só a pagina e o tamanho', () => {
      const { api, http } = setup();
      const received: unknown[] = [];

      api.listManagers(DEFAULT_MANAGERS_QUERY).subscribe((page) => received.push(page));

      const request = http.expectOne((candidate) => candidate.url === MANAGERS_URL);
      expect(request.request.method).toBe('GET');
      expect(request.request.params.toString()).toBe('page=1&pageSize=20');
      request.flush(managersPage([manager()]));
      expect(received).toEqual([managersPage([manager()])]);
    });

    it('manda a situacao e a busca preenchidas, com a busca sem espacos nas pontas', () => {
      const { api, http } = setup();

      api
        .listManagers({ status: 'suspended', search: '  ana ', page: 3, pageSize: 20 })
        .subscribe();

      const request = http.expectOne((candidate) => candidate.url === MANAGERS_URL);
      expect(request.request.params.toString()).toBe(
        'status=suspended&search=ana&page=3&pageSize=20',
      );
      request.flush(managersPage([]));
    });

    it('nao manda a busca quando so tem espacos', () => {
      const { api, http } = setup();

      api.listManagers({ ...DEFAULT_MANAGERS_QUERY, search: '   ' }).subscribe();

      const request = http.expectOne((candidate) => candidate.url === MANAGERS_URL);
      expect(request.request.params.has('search')).toBe(false);
      request.flush(managersPage([]));
    });
  });

  it('suspende um sindico pelo id, com o motivo', () => {
    const { api, http } = setup();

    api.suspend('abc-1', 'Uso indevido').subscribe();

    const request = http.expectOne(`${MANAGERS_URL}/abc-1/suspend`);
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ reason: 'Uso indevido' });
    request.flush(null, NO_CONTENT);
  });

  it('reativa um sindico pelo id', () => {
    const { api, http } = setup();

    api.reactivate('abc-1').subscribe();

    const request = http.expectOne(`${MANAGERS_URL}/abc-1/reactivate`);
    expect(request.request.method).toBe('POST');
    request.flush(null, NO_CONTENT);
  });
});
