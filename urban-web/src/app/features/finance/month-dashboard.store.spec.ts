import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { monthDashboard } from '../../testing/month-dashboard.fixtures';
import { MonthDashboardStore } from './month-dashboard.store';

const PATH = (month: string): string => `/api/finance/months/${month}/dashboard`;

type Store = InstanceType<typeof MonthDashboardStore>;

function setup(): { store: Store; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), MonthDashboardStore],
  });
  return {
    store: TestBed.inject(MonthDashboardStore),
    http: TestBed.inject(HttpTestingController),
  };
}

function fail(status: number): [unknown, { status: number; statusText: string }] {
  return [problem(status, 'request.invalid'), { status, statusText: 'Erro' }];
}

describe('MonthDashboardStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('começa carregando, sem mês e sem dados', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.month()).toBe('');
    expect(store.data()).toBeNull();
  });

  describe('show', () => {
    it('lê o painel do mês e fica pronto', async () => {
      const { store, http } = setup();
      const data = monthDashboard({ month: '2026-11' });

      const loading = store.show('2026-11');
      expect(store.status()).toBe('loading');
      expect(store.month()).toBe('2026-11');
      const request = http.expectOne(PATH('2026-11'));
      expect(request.request.method).toBe('GET');
      request.flush(data);
      await loading;

      expect(store.status()).toBe('ready');
      expect(store.data()).toEqual(data);
    });

    it('volta a carregar ao trocar de mês e troca os dados', async () => {
      const { store, http } = setup();
      const first = store.show('2026-11');
      http.expectOne(PATH('2026-11')).flush(monthDashboard({ month: '2026-11' }));
      await first;

      const second = store.show('2026-10');
      expect(store.status()).toBe('loading');
      expect(store.month()).toBe('2026-10');
      http.expectOne(PATH('2026-10')).flush(monthDashboard({ month: '2026-10' }));
      await second;

      expect(store.status()).toBe('ready');
      expect(store.data()?.month).toBe('2026-10');
    });

    it('fica em erro quando a leitura falha e se recupera ao tentar de novo', async () => {
      const { store, http } = setup();
      const failing = store.show('2026-11');
      http.expectOne(PATH('2026-11')).flush(...fail(500));
      await failing;

      expect(store.status()).toBe('error');

      const retry = store.show('2026-11');
      expect(store.status()).toBe('loading');
      http.expectOne(PATH('2026-11')).flush(monthDashboard());
      await retry;

      expect(store.status()).toBe('ready');
    });

    it('ignora a resposta de um mês que a pessoa já deixou para trás', async () => {
      const { store, http } = setup();
      const slow = store.show('2026-10');
      const slowRequest = http.expectOne(PATH('2026-10'));
      const current = store.show('2026-11');
      http.expectOne(PATH('2026-11')).flush(monthDashboard({ month: '2026-11' }));
      await current;

      slowRequest.flush(monthDashboard({ month: '2026-10' }));
      await slow;
      await flushPromises();

      expect(store.month()).toBe('2026-11');
      expect(store.data()?.month).toBe('2026-11');
      expect(store.status()).toBe('ready');
    });

    it('ignora a falha de um mês que a pessoa já deixou para trás', async () => {
      const { store, http } = setup();
      const slow = store.show('2026-10');
      const slowRequest = http.expectOne(PATH('2026-10'));
      const current = store.show('2026-11');
      http.expectOne(PATH('2026-11')).flush(monthDashboard({ month: '2026-11' }));
      await current;

      slowRequest.flush(...fail(500));
      await slow;
      await flushPromises();

      expect(store.status()).toBe('ready');
      expect(store.data()?.month).toBe('2026-11');
    });
  });
});
