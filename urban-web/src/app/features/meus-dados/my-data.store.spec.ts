import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { resident } from '../../testing/residents.fixtures';
import { MyDataStore } from './my-data.store';

const ROUTE = '/api/residents/me';

function setup(): { store: InstanceType<typeof MyDataStore>; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), MyDataStore],
  });
  return { store: TestBed.inject(MyDataStore), http: TestBed.inject(HttpTestingController) };
}

describe('MyDataStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('começa em "loading", sem cadastro', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.resident()).toBeNull();
  });

  it('busca o próprio cadastro em GET /api/residents/me e vira "ready"', async () => {
    const { store, http } = setup();
    const mine = resident();

    const loading = store.load();
    const request = http.expectOne(ROUTE);
    expect(request.request.method).toBe('GET');
    request.flush(mine);
    await loading;

    expect(store.status()).toBe('ready');
    expect(store.resident()).toEqual(mine);
  });

  it('vira "not-found" quando a conta não tem cadastro de morador (404)', async () => {
    const { store, http } = setup();

    const loading = store.load();
    http
      .expectOne(ROUTE)
      .flush({ code: 'condominium.resident.not_found' }, { status: 404, statusText: 'Not Found' });
    await loading;

    expect(store.status()).toBe('not-found');
    expect(store.resident()).toBeNull();
  });

  it('vira "error" nas outras falhas, e carregar de novo funciona', async () => {
    const { store, http } = setup();

    const failing = store.load();
    http.expectOne(ROUTE).flush({ code: 'x' }, { status: 500, statusText: 'Erro' });
    await failing;
    expect(store.status()).toBe('error');

    const mine = resident();
    const retrying = store.load();
    expect(store.status()).toBe('loading');
    http.expectOne(ROUTE).flush(mine);
    await retrying;

    expect(store.status()).toBe('ready');
    expect(store.resident()).toEqual(mine);
  });
});
