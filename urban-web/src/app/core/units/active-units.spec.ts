import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { flushPromises } from '../../testing/async';
import { unit, unitPage } from '../../testing/units.fixtures';
import { fetchActiveUnits } from './active-units';
import { UnitsApi } from './units.api';

function setup(): { api: UnitsApi; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting()],
  });
  return { api: TestBed.inject(UnitsApi), http: TestBed.inject(HttpTestingController) };
}

function pageRequest(http: HttpTestingController) {
  return http.expectOne((request) => request.url === '/api/units');
}

describe('fetchActiveUnits', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('pede as unidades ativas de 200 em 200, sem filtro', async () => {
    const { api, http } = setup();

    const result = fetchActiveUnits(api);
    const request = pageRequest(http);
    request.flush(unitPage([unit()]));
    await result;

    expect(new URLSearchParams(request.request.params.toString()).toString()).toBe(
      'status=active&page=1&pageSize=200',
    );
  });

  it('junta as páginas até somar o total', async () => {
    const { api, http } = setup();
    const first = [unit(), unit()];
    const second = [unit()];

    const result = fetchActiveUnits(api);
    pageRequest(http).flush(unitPage(first, 3, 1, 2));
    await flushPromises();
    const next = pageRequest(http);
    next.flush(unitPage(second, 3, 2, 2));

    expect(await result).toEqual([...first, ...second]);
    expect(new URLSearchParams(next.request.params.toString()).get('page')).toBe('2');
  });

  it('para quando uma página vem vazia, mesmo que o total prometa mais', async () => {
    const { api, http } = setup();

    const result = fetchActiveUnits(api);
    pageRequest(http).flush(unitPage([], 5));

    expect(await result).toEqual([]);
  });

  it('repassa a falha da API', async () => {
    const { api, http } = setup();

    const result = fetchActiveUnits(api);
    pageRequest(http).flush(null, { status: 500, statusText: 'Erro' });

    await expect(result).rejects.toBeDefined();
  });
});
