import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { resident, residentPage } from '../../testing/residents.fixtures';
import { DEFAULT_RESIDENTS_QUERY, ResidentsQuery } from './residents.models';
import { ResidentsApi } from './residents.api';

describe('ResidentsApi', () => {
  let api: ResidentsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(ResidentsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function listParams(query: ResidentsQuery): URLSearchParams {
    api.list(query).subscribe();
    const request = http.expectOne(
      (candidate) => candidate.url === '/api/residents' && candidate.method === 'GET',
    );
    request.flush(residentPage([]));
    return new URLSearchParams(request.request.params.toString());
  }

  describe('list', () => {
    it('pede GET /api/residents e devolve a página', () => {
      const page = residentPage([resident()], 1);
      let received: unknown;

      api.list(DEFAULT_RESIDENTS_QUERY).subscribe((value) => (received = value));
      http.expectOne((request) => request.url === '/api/residents').flush(page);

      expect(received).toEqual(page);
    });

    it('sempre manda a situação, a página e o tamanho', () => {
      const params = listParams(DEFAULT_RESIDENTS_QUERY);

      expect(params.get('status')).toBe('active');
      expect(params.get('page')).toBe('1');
      expect(params.get('pageSize')).toBe('50');
    });

    it.each(['active', 'former', 'all'] as const)('manda a situação "%s" como está', (status) => {
      expect(listParams({ ...DEFAULT_RESIDENTS_QUERY, status }).get('status')).toBe(status);
    });

    it('só manda a busca quando tem texto, e apara os espaços das pontas', () => {
      expect(listParams({ ...DEFAULT_RESIDENTS_QUERY, search: '' }).has('search')).toBe(false);
      expect(listParams({ ...DEFAULT_RESIDENTS_QUERY, search: '   ' }).has('search')).toBe(false);
      expect(listParams({ ...DEFAULT_RESIDENTS_QUERY, search: '  maria ' }).get('search')).toBe(
        'maria',
      );
    });

    it('manda a página pedida', () => {
      const params = listParams({ ...DEFAULT_RESIDENTS_QUERY, page: 3 });

      expect(params.get('page')).toBe('3');
    });
  });
});
