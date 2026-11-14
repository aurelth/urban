import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { resident, residentPage } from '../../testing/residents.fixtures';
import {
  DEFAULT_RESIDENTS_QUERY,
  EndOccupancyPayload,
  OccupancyPayload,
  ResidentPayload,
  ResidentsQuery,
  UpdateResidentPayload,
} from './residents.models';
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

  describe('get', () => {
    it('pede GET /api/residents/{id} e devolve o morador', () => {
      const found = resident();
      let received: unknown;

      api.get(found.id).subscribe((value) => (received = value));
      const request = http.expectOne(`/api/residents/${found.id}`);
      request.flush(found);

      expect(request.request.method).toBe('GET');
      expect(received).toEqual(found);
    });
  });

  describe('create', () => {
    const payload: ResidentPayload = {
      fullName: 'Maria Souza',
      cpf: '52998224725',
      birthDate: '1990-05-20',
      phone: '47998765432',
      email: null,
      profession: 'Engenheira',
      emergencyContact: { name: 'Carlos Souza', relationship: 'Irmão', phone: '4733334444' },
      unitId: '0199aaaa-0000-7000-8000-000000000001',
      kind: 'owner',
      startDate: '2024-03-01',
    };

    it('manda POST /api/residents com o corpo como está e devolve o morador criado', () => {
      const created = resident();
      let received: unknown;

      api.create(payload).subscribe((value) => (received = value));
      const request = http.expectOne('/api/residents');
      request.flush(created, { status: 201, statusText: 'Created' });

      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual(payload);
      expect(received).toEqual(created);
    });
  });

  describe('update', () => {
    it('manda PUT /api/residents/{id} com a versão e devolve o morador', () => {
      const updated = resident();
      const payload: UpdateResidentPayload = {
        version: '7',
        fullName: 'Maria Souza',
        birthDate: '1990-05-20',
        phone: '47998765432',
        email: 'maria@exemplo.com',
        profession: 'Engenheira',
        emergencyContact: { name: 'Carlos Souza', relationship: 'Irmão', phone: '4733334444' },
      };
      let received: unknown;

      api.update(updated.id, payload).subscribe((value) => (received = value));
      const request = http.expectOne(`/api/residents/${updated.id}`);
      request.flush(updated);

      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual(payload);
      expect(received).toEqual(updated);
    });
  });

  describe('addOccupancy', () => {
    it('manda POST /api/residents/{id}/occupancies com o corpo como está e devolve o morador', () => {
      const updated = resident();
      const payload: OccupancyPayload = {
        unitId: '0199aaaa-0000-7000-8000-000000000002',
        kind: 'tenant',
        startDate: '2026-01-05',
      };
      let received: unknown;

      api.addOccupancy(updated.id, payload).subscribe((value) => (received = value));
      const request = http.expectOne(`/api/residents/${updated.id}/occupancies`);
      request.flush(updated, { status: 201, statusText: 'Created' });

      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual(payload);
      expect(received).toEqual(updated);
    });
  });

  describe('endOccupancy', () => {
    it('manda POST .../occupancies/{occupancyId}/end com a data e devolve o morador', () => {
      const updated = resident();
      const payload: EndOccupancyPayload = { endDate: '2026-11-10' };
      let received: unknown;

      api.endOccupancy(updated.id, 'occ-1', payload).subscribe((value) => (received = value));
      const request = http.expectOne(`/api/residents/${updated.id}/occupancies/occ-1/end`);
      request.flush(updated);

      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual(payload);
      expect(received).toEqual(updated);
    });
  });
});
