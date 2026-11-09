import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ReadingInput } from '../../core/finance/finance.models';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { meterReading, monthReadings, unitReadings } from '../../testing/finance.fixtures';
import { ReadingsStore } from './readings.store';

const URL = '/api/finance/months/2026-11/readings';
const SAVED: ReadingInput[] = [{ unitId: 'u1', service: 'water', previous: 100, current: 112.5 }];

function setup(): { store: InstanceType<typeof ReadingsStore>; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), ReadingsStore],
  });
  return { store: TestBed.inject(ReadingsStore), http: TestBed.inject(HttpTestingController) };
}

async function shown(
  store: InstanceType<typeof ReadingsStore>,
  http: HttpTestingController,
): Promise<void> {
  const showing = store.show('2026-11');
  http.expectOne({ method: 'GET', url: URL }).flush(monthReadings());
  await showing;
}

function fail(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

describe('ReadingsStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('comeca carregando, sem mes e sem leituras', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.month()).toBe('');
    expect(store.data()).toBeNull();
    expect(store.busy()).toBe(false);
  });

  describe('show', () => {
    it('le as leituras do mes e fica pronto', async () => {
      const { store, http } = setup();

      const showing = store.show('2026-11');
      expect(store.status()).toBe('loading');
      expect(store.month()).toBe('2026-11');
      http.expectOne({ method: 'GET', url: URL }).flush(monthReadings({ hasGas: true }));
      await showing;

      expect(store.status()).toBe('ready');
      expect(store.data()?.hasGas).toBe(true);
    });

    it('vira "erro" quando a leitura falha e pode tentar de novo', async () => {
      const { store, http } = setup();
      const first = store.show('2026-11');
      http.expectOne({ method: 'GET', url: URL }).flush(null, { status: 500, statusText: 'Erro' });
      await first;
      expect(store.status()).toBe('error');

      await shown(store, http);

      expect(store.status()).toBe('ready');
    });

    it('troca de mes lendo o novo mes', async () => {
      const { store, http } = setup();
      await shown(store, http);

      const showing = store.show('2026-12');
      http
        .expectOne({ method: 'GET', url: '/api/finance/months/2026-12/readings' })
        .flush(monthReadings({ month: '2026-12', isClosed: true }));
      await showing;

      expect(store.month()).toBe('2026-12');
      expect(store.data()?.isClosed).toBe(true);
    });
  });

  describe('save', () => {
    it('grava as leituras, mostra as que a API devolve e responde nulo', async () => {
      const { store, http } = setup();
      await shown(store, http);

      const saving = store.save(SAVED);
      expect(store.busy()).toBe(true);
      const put = http.expectOne({ method: 'PUT', url: URL });
      expect(put.request.body).toEqual({ readings: SAVED });
      put.flush(
        monthReadings({
          units: [
            unitReadings({ unitId: 'u1', water: meterReading({ previous: 100, current: 112.5 }) }),
          ],
        }),
      );

      expect(await saving).toBeNull();
      expect(store.busy()).toBe(false);
      expect(store.data()?.units[0].water?.current).toBe(112.5);
    });

    it('devolve o problema e mantem a tela quando a API recusa uma leitura', async () => {
      const { store, http } = setup();
      await shown(store, http);
      const before = store.data();

      const saving = store.save(SAVED);
      http
        .expectOne({ method: 'PUT', url: URL })
        .flush(...fail(400, 'consumption.reading_invalid'));
      const result = await saving;

      expect(result?.code).toBe('consumption.reading_invalid');
      expect(store.data()).toBe(before);
      expect(store.busy()).toBe(false);
    });

    it('le o mes de novo quando ele ja foi fechado em outro lugar', async () => {
      const { store, http } = setup();
      await shown(store, http);

      const saving = store.save(SAVED);
      http.expectOne({ method: 'PUT', url: URL }).flush(...fail(409, 'closing.month_is_closed'));
      await flushPromises();
      http.expectOne({ method: 'GET', url: URL }).flush(monthReadings({ isClosed: true }));

      expect((await saving)?.code).toBe('closing.month_is_closed');
      expect(store.data()?.isClosed).toBe(true);
    });

    it('le o mes de novo quando uma unidade nao existe mais', async () => {
      const { store, http } = setup();
      await shown(store, http);

      const saving = store.save(SAVED);
      http.expectOne({ method: 'PUT', url: URL }).flush(...fail(404, 'consumption.unit_not_found'));
      await flushPromises();
      http.expectOne({ method: 'GET', url: URL }).flush(monthReadings());

      expect((await saving)?.code).toBe('consumption.unit_not_found');
    });

    it('vira "erro" quando nao consegue reler o mes depois de uma recusa', async () => {
      const { store, http } = setup();
      await shown(store, http);

      const saving = store.save(SAVED);
      http.expectOne({ method: 'PUT', url: URL }).flush(...fail(409, 'closing.month_is_closed'));
      await flushPromises();
      http.expectOne({ method: 'GET', url: URL }).flush(null, { status: 500, statusText: 'Erro' });
      await saving;

      expect(store.status()).toBe('error');
    });
  });
});
