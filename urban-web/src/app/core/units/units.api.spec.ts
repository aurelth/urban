import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import {
  BatchCreated,
  DEFAULT_UNITS_QUERY,
  ApportionmentChanged,
  Unit,
  UnitPage,
  UnitPayload,
  UnitsQuery,
  UnitsSummary,
} from './units.models';
import { UnitsApi } from './units.api';

const UNIT: Unit = {
  id: '0199aaaa-0000-7000-8000-000000000001',
  block: 'A',
  number: '101',
  type: 'apartment',
  idealFraction: 0.0125,
  area: 52.5,
  isActive: true,
  deactivatedAt: null,
  version: '41',
};

const PAYLOAD: UnitPayload = {
  block: 'A',
  number: '101',
  type: 'apartment',
  idealFraction: 0.0125,
  area: 52.5,
};

describe('UnitsApi', () => {
  let api: UnitsApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(UnitsApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  describe('list', () => {
    function list(query: UnitsQuery): URLSearchParams {
      api.list(query).subscribe();
      const request = http.expectOne((candidate) => candidate.url === '/api/units');
      expect(request.request.method).toBe('GET');
      request.flush({ items: [], total: 0, page: 1, pageSize: 50 });
      return new URLSearchParams(request.request.params.toString());
    }

    it('pede so a situacao e a pagina quando nao ha filtro', () => {
      const params = list(DEFAULT_UNITS_QUERY);

      expect(params.toString()).toBe('status=active&page=1&pageSize=50');
    });

    it('manda cada filtro preenchido, com o texto sem espacos nas pontas', () => {
      const params = list({
        status: 'deactivated',
        type: 'house',
        block: ' Rua 2 ',
        search: ' 10 ',
        page: 3,
        pageSize: 20,
      });

      expect(params.get('status')).toBe('deactivated');
      expect(params.get('type')).toBe('house');
      expect(params.get('block')).toBe('Rua 2');
      expect(params.get('search')).toBe('10');
      expect(params.get('page')).toBe('3');
      expect(params.get('pageSize')).toBe('20');
    });

    it('nao manda filtro vazio nem so de espacos', () => {
      const params = list({ ...DEFAULT_UNITS_QUERY, type: null, block: '  ', search: '' });

      expect(params.has('type')).toBe(false);
      expect(params.has('block')).toBe(false);
      expect(params.has('search')).toBe(false);
    });

    it('devolve a pagina como a API respondeu', () => {
      const page: UnitPage = { items: [UNIT], total: 1, page: 1, pageSize: 50 };
      let result: UnitPage | undefined;

      api.list(DEFAULT_UNITS_QUERY).subscribe((value) => (result = value));
      http.expectOne((candidate) => candidate.url === '/api/units').flush(page);

      expect(result).toEqual(page);
    });
  });

  it('busca o resumo com GET', () => {
    const summary: UnitsSummary = {
      active: 10,
      deactivated: 1,
      houses: 0,
      apartments: 10,
      idealFractionTotal: 0.5,
      withoutIdealFraction: 2,
      withoutArea: 10,
      apportionmentMethod: 'idealFraction',
      apportionmentVersion: '12',
      readiness: [{ method: 'idealFraction', ready: false }],
    };
    let result: UnitsSummary | undefined;

    api.summary().subscribe((value) => (result = value));
    const request = http.expectOne('/api/units/summary');
    expect(request.request.method).toBe('GET');
    request.flush(summary);

    expect(result).toEqual(summary);
  });

  it('cadastra uma unidade com POST', () => {
    let result: Unit | undefined;

    api.create(PAYLOAD).subscribe((value) => (result = value));
    const request = http.expectOne('/api/units');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual(PAYLOAD);
    request.flush(UNIT);

    expect(result).toEqual(UNIT);
  });

  it('edita uma unidade com PUT, levando a versao lida', () => {
    api.update(UNIT.id, { ...PAYLOAD, version: '41' }).subscribe();

    const request = http.expectOne(`/api/units/${UNIT.id}`);
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ ...PAYLOAD, version: '41' });
    request.flush(UNIT);
  });

  it.each(['deactivate', 'reactivate'] as const)(
    '%s chama o POST da acao com a versao',
    (action) => {
      api[action](UNIT.id, '41').subscribe();

      const request = http.expectOne(`/api/units/${UNIT.id}/${action}`);
      expect(request.request.method).toBe('POST');
      expect(request.request.body).toEqual({ version: '41' });
      request.flush(UNIT);
    },
  );

  it('cadastra o lote com um unico POST', () => {
    let result: BatchCreated | undefined;

    api
      .createBatch([PAYLOAD, { ...PAYLOAD, number: '102' }])
      .subscribe((value) => (result = value));
    const request = http.expectOne('/api/units/batch');
    expect(request.request.method).toBe('POST');
    expect(request.request.body).toEqual({ units: [PAYLOAD, { ...PAYLOAD, number: '102' }] });
    request.flush({ created: 2 });

    expect(result).toEqual({ created: 2 });
  });

  it('troca o metodo de rateio com PUT, levando a versao', () => {
    const changed: ApportionmentChanged = { method: 'area', version: '13' };
    let result: ApportionmentChanged | undefined;

    api.changeMethod('12', 'area').subscribe((value) => (result = value));
    const request = http.expectOne('/api/units/apportionment-method');
    expect(request.request.method).toBe('PUT');
    expect(request.request.body).toEqual({ version: '12', method: 'area' });
    request.flush(changed);

    expect(result).toEqual(changed);
  });
});
