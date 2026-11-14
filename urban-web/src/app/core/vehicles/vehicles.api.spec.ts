import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { vehicle, vehiclePage } from '../../testing/vehicles.fixtures';
import { VehiclesApi } from './vehicles.api';
import { DEFAULT_VEHICLES_QUERY, Vehicle, VehiclePage, VehiclesQuery } from './vehicles.models';

const PAYLOAD = {
  unitId: '0199aaaa-0000-7000-8000-000000000001',
  plate: 'ABC1D23',
  model: 'Fiat Argo',
  color: 'Prata',
};

describe('VehiclesApi', () => {
  let api: VehiclesApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(VehiclesApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe('list', () => {
    function list(query: VehiclesQuery): URLSearchParams {
      api.list(query).subscribe();
      const request = http.expectOne((candidate) => candidate.url === '/api/vehicles');
      expect(request.request.method).toBe('GET');
      request.flush(vehiclePage([]));
      return new URLSearchParams(request.request.params.toString());
    }

    it('pede só a página quando não há filtro', () => {
      expect(list(DEFAULT_VEHICLES_QUERY).toString()).toBe('page=1&pageSize=50');
    });

    it('manda a unidade e a busca preenchidas, com o texto sem espaços nas pontas', () => {
      const params = list({ unitId: 'u-1', search: ' abc ', page: 2, pageSize: 20 });

      expect(params.get('unitId')).toBe('u-1');
      expect(params.get('search')).toBe('abc');
      expect(params.get('page')).toBe('2');
      expect(params.get('pageSize')).toBe('20');
    });

    it('não manda busca vazia nem só de espaços', () => {
      const params = list({ ...DEFAULT_VEHICLES_QUERY, search: '   ' });

      expect(params.has('search')).toBe(false);
      expect(params.has('unitId')).toBe(false);
    });

    it('devolve a página como a API respondeu', () => {
      const page: VehiclePage = vehiclePage([vehicle()]);
      let result: VehiclePage | undefined;

      api.list(DEFAULT_VEHICLES_QUERY).subscribe((value) => (result = value));
      http.expectOne((candidate) => candidate.url === '/api/vehicles').flush(page);

      expect(result).toEqual(page);
    });
  });

  it('busca um veículo com GET', () => {
    const found = vehicle();
    let result: Vehicle | undefined;

    api.get(found.id).subscribe((value) => (result = value));
    const request = http.expectOne(`/api/vehicles/${found.id}`);
    expect(request.request.method).toBe('GET');
    request.flush(found);

    expect(result).toEqual(found);
  });

  it('cadastra um veículo com POST', () => {
    const created = vehicle();
    let result: Vehicle | undefined;

    api.create(PAYLOAD).subscribe((value) => (result = value));
    const request = http.expectOne('/api/vehicles');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(PAYLOAD);
    request.flush(created);

    expect(result).toEqual(created);
  });

  it('altera um veículo com PUT, levando a versão lida', () => {
    const changed = vehicle();

    api.update(changed.id, { ...PAYLOAD, version: '41' }).subscribe();
    const request = http.expectOne(`/api/vehicles/${changed.id}`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ ...PAYLOAD, version: '41' });
    request.flush(changed);
  });

  it('exclui um veículo com DELETE', () => {
    let done = false;

    api.delete('v-1').subscribe(() => (done = true));
    const request = http.expectOne('/api/vehicles/v-1');
    expect(request.request.method).toBe('DELETE');
    request.flush(null, { status: 204, statusText: 'No Content' });

    expect(done).toBe(true);
  });
});
