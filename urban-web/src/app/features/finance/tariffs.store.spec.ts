import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ConsumptionTariffPayload } from '../../core/finance/finance.models';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { consumptionTariff, tariffBand } from '../../testing/finance.fixtures';
import { TariffsStore } from './tariffs.store';

const NEWER = consumptionTariff({ validFrom: '2026-11' });
const OLDER = consumptionTariff({ validFrom: '2026-03' });

const PAYLOAD: ConsumptionTariffPayload = {
  water: [tariffBand({ pricePerCubicMeter: 6 })],
  sewage: null,
  sewagePercent: 100,
  gas: null,
};

const TARIFFS_URL = '/api/finance/consumption-tariffs';
const SETTINGS_URL = '/api/finance/settings';

function setup(): { store: InstanceType<typeof TariffsStore>; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), TariffsStore],
  });
  return { store: TestBed.inject(TariffsStore), http: TestBed.inject(HttpTestingController) };
}

function answerFetch(
  http: HttpTestingController,
  tariffs = [NEWER, OLDER],
  reserveFundPercent = 5,
): void {
  http.expectOne({ method: 'GET', url: TARIFFS_URL }).flush(tariffs);
  http.expectOne({ method: 'GET', url: SETTINGS_URL }).flush({ reserveFundPercent });
}

async function loaded(
  store: InstanceType<typeof TariffsStore>,
  http: HttpTestingController,
): Promise<void> {
  const loading = store.load();
  answerFetch(http);
  await loading;
}

function fail(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

describe('TariffsStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('comeca carregando, sem tarifas e sem fundo de reserva', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.tariffs()).toEqual([]);
    expect(store.reserveFundPercent()).toBe(0);
    expect(store.busy()).toBe(false);
  });

  describe('load', () => {
    it('busca as tarifas e o percentual juntos, na ordem que a API manda', async () => {
      const { store, http } = setup();

      await loaded(store, http);

      expect(store.status()).toBe('ready');
      expect(store.tariffs().map((tariff) => tariff.validFrom)).toEqual(['2026-11', '2026-03']);
      expect(store.reserveFundPercent()).toBe(5);
    });

    it('vira "erro" quando alguma busca falha e pode tentar de novo', async () => {
      const { store, http } = setup();
      const first = store.load();
      http.expectOne({ method: 'GET', url: TARIFFS_URL }).flush(null, {
        status: 500,
        statusText: 'Erro',
      });
      http.expectOne({ method: 'GET', url: SETTINGS_URL }).flush({ reserveFundPercent: 0 });
      await first;
      expect(store.status()).toBe('error');

      await loaded(store, http);

      expect(store.status()).toBe('ready');
    });
  });

  describe('saveTariff', () => {
    it('grava a tarifa do mes, atualiza a tela e devolve nulo', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const saving = store.saveTariff('2027-01', PAYLOAD);
      expect(store.busy()).toBe(true);
      const put = http.expectOne({ method: 'PUT', url: `${TARIFFS_URL}/2027-01` });
      expect(put.request.body).toEqual(PAYLOAD);
      put.flush(consumptionTariff({ validFrom: '2027-01' }));
      await flushPromises();
      answerFetch(http, [consumptionTariff({ validFrom: '2027-01' }), NEWER, OLDER]);

      expect(await saving).toBeNull();
      expect(store.tariffs()).toHaveLength(3);
      expect(store.busy()).toBe(false);
    });

    it('devolve o problema sem atualizar quando a API recusa o conteudo', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const saving = store.saveTariff('2027-01', PAYLOAD);
      http
        .expectOne({ method: 'PUT', url: `${TARIFFS_URL}/2027-01` })
        .flush(...fail(400, 'consumption.bands_invalid'));
      const result = await saving;

      expect(result?.code).toBe('consumption.bands_invalid');
      expect(store.busy()).toBe(false);
      expect(store.tariffs()).toHaveLength(2);
    });

    it('vira "erro" quando a tela nao consegue atualizar depois de gravar', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const saving = store.saveTariff('2027-01', PAYLOAD);
      http.expectOne({ method: 'PUT', url: `${TARIFFS_URL}/2027-01` }).flush({});
      await flushPromises();
      http.expectOne({ method: 'GET', url: TARIFFS_URL }).flush(null, {
        status: 500,
        statusText: 'Erro',
      });
      http.expectOne({ method: 'GET', url: SETTINGS_URL }).flush({ reserveFundPercent: 5 });
      await saving;

      expect(store.status()).toBe('error');
    });
  });

  describe('deleteTariff', () => {
    it('apaga a tarifa do mes e atualiza a tela', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const deleting = store.deleteTariff('2026-03');
      http.expectOne({ method: 'DELETE', url: `${TARIFFS_URL}/2026-03` }).flush(null, {
        status: 204,
        statusText: 'No Content',
      });
      await flushPromises();
      answerFetch(http, [NEWER]);

      expect(await deleting).toBeNull();
      expect(store.tariffs()).toHaveLength(1);
    });

    it('atualiza a tela quando a tarifa ja nao existe e devolve o problema', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const deleting = store.deleteTariff('2026-03');
      http
        .expectOne({ method: 'DELETE', url: `${TARIFFS_URL}/2026-03` })
        .flush(...fail(404, 'consumption.tariff_not_found'));
      await flushPromises();
      answerFetch(http, [NEWER]);

      expect((await deleting)?.code).toBe('consumption.tariff_not_found');
      expect(store.tariffs()).toHaveLength(1);
    });
  });

  describe('setReserveFundPercent', () => {
    it('grava o percentual e mostra o novo valor', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const saving = store.setReserveFundPercent(2.5);
      const put = http.expectOne({ method: 'PUT', url: SETTINGS_URL });
      expect(put.request.body).toEqual({ reserveFundPercent: 2.5 });
      put.flush({ reserveFundPercent: 2.5 });
      await flushPromises();
      answerFetch(http, [NEWER, OLDER], 2.5);

      expect(await saving).toBeNull();
      expect(store.reserveFundPercent()).toBe(2.5);
    });

    it('devolve o problema quando a API recusa o percentual', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const saving = store.setReserveFundPercent(150);
      http
        .expectOne({ method: 'PUT', url: SETTINGS_URL })
        .flush(...fail(400, 'consumption.reserve_percent_invalid'));

      expect((await saving)?.code).toBe('consumption.reserve_percent_invalid');
      expect(store.reserveFundPercent()).toBe(5);
    });
  });
});
