import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { entry, groupTotal, monthEntries } from '../../testing/finance.fixtures';
import { MonthStore } from './month.store';

function setup(): { store: InstanceType<typeof MonthStore>; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), MonthStore],
  });
  return { store: TestBed.inject(MonthStore), http: TestBed.inject(HttpTestingController) };
}

function fail(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

describe('MonthStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('comeca carregando, sem lancamentos', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.data()).toBeNull();
    expect(store.month()).toBe('');
    expect(store.busy()).toBe(false);
  });

  describe('show', () => {
    it('le o mes pedido e passa a pronto', async () => {
      const { store, http } = setup();
      const month = monthEntries({ month: '2026-10', entries: [entry()], total: 4200 });

      const showing = store.show('2026-10');
      expect(store.status()).toBe('loading');
      http.expectOne('/api/finance/months/2026-10').flush(month);
      await showing;

      expect(store.status()).toBe('ready');
      expect(store.month()).toBe('2026-10');
      expect(store.data()).toEqual(month);
    });

    it('vira "erro" quando a leitura falha e pode tentar de novo', async () => {
      const { store, http } = setup();
      const first = store.show('2026-11');
      http
        .expectOne('/api/finance/months/2026-11')
        .flush(null, { status: 500, statusText: 'Erro' });
      await first;
      expect(store.status()).toBe('error');

      const second = store.show('2026-11');
      http.expectOne('/api/finance/months/2026-11').flush(monthEntries());
      await second;

      expect(store.status()).toBe('ready');
    });

    it('agrupa os lancamentos na ordem dos grupos que a API devolveu', async () => {
      const { store, http } = setup();
      const first = groupTotal({ groupId: 'g1', groupName: 'Contratos', total: 4000 });
      const second = groupTotal({ groupId: 'g2', groupName: 'Obras', total: 100 });
      const showing = store.show('2026-11');
      http.expectOne('/api/finance/months/2026-11').flush(
        monthEntries({
          groups: [first, second],
          entries: [
            entry({ groupId: 'g2', description: 'Elevador' }),
            entry({ groupId: 'g1', description: 'Portaria' }),
            entry({ groupId: 'g1', description: 'Zeladoria' }),
          ],
        }),
      );
      await showing;

      const sections = store.sections();
      expect(sections.map((section) => section.group.groupName)).toEqual(['Contratos', 'Obras']);
      expect(sections[0].entries.map((item) => item.description)).toEqual([
        'Portaria',
        'Zeladoria',
      ]);
      expect(sections[1].entries.map((item) => item.description)).toEqual(['Elevador']);
    });
  });

  describe('open', () => {
    it('abre o mes e mostra o que foi gerado', async () => {
      const { store, http } = setup();
      const showing = store.show('2026-11');
      http.expectOne('/api/finance/months/2026-11').flush(monthEntries());
      await showing;

      const opening = store.open();
      expect(store.busy()).toBe(true);
      const post = http.expectOne({ method: 'POST', url: '/api/finance/months/2026-11/open' });
      post.flush(monthEntries({ entries: [entry()], total: 4200 }));

      expect(await opening).toBeNull();
      expect(store.data()?.entries).toHaveLength(1);
      expect(store.busy()).toBe(false);
    });

    it('devolve o problema quando a API recusa', async () => {
      const { store, http } = setup();
      const showing = store.show('2027-03');
      http.expectOne('/api/finance/months/2027-03').flush(monthEntries({ month: '2027-03' }));
      await showing;

      const opening = store.open();
      http
        .expectOne({ method: 'POST', url: '/api/finance/months/2027-03/open' })
        .flush(...fail(422, 'month.too_far_ahead'));

      expect((await opening)?.code).toBe('month.too_far_ahead');
      expect(store.data()?.entries).toEqual([]);
    });
  });

  describe('setAmount', () => {
    it('grava o valor e le o mes de novo para refazer os totais', async () => {
      const { store, http } = setup();
      const pending = entry({ isPending: true, amount: null, version: '30' });
      const showing = store.show('2026-11');
      http.expectOne('/api/finance/months/2026-11').flush(monthEntries({ entries: [pending] }));
      await showing;

      const saving = store.setAmount(pending, 812.34);
      const put = http.expectOne({
        method: 'PUT',
        url: `/api/finance/entries/${pending.id}/amount`,
      });
      expect(put.request.body).toEqual({ version: '30', amount: 812.34 });
      put.flush(entry({ amount: 812.34 }));
      await flushPromises();
      http
        .expectOne('/api/finance/months/2026-11')
        .flush(monthEntries({ entries: [entry({ amount: 812.34 })], total: 812.34 }));

      expect(await saving).toBeNull();
      expect(store.data()?.total).toBe(812.34);
    });

    it.each([
      [409, 'concurrency.conflict'],
      [404, 'expense_entry.not_found'],
    ])(
      'devolve o problema e le o mes de novo quando esta desatualizado (%i)',
      async (status, code) => {
        const { store, http } = setup();
        const target = entry();
        const showing = store.show('2026-11');
        http.expectOne('/api/finance/months/2026-11').flush(monthEntries({ entries: [target] }));
        await showing;

        const saving = store.setAmount(target, 10);
        http
          .expectOne({ method: 'PUT', url: `/api/finance/entries/${target.id}/amount` })
          .flush(...fail(status, code));
        await flushPromises();
        http.expectOne('/api/finance/months/2026-11').flush(monthEntries());

        expect((await saving)?.code).toBe(code);
        expect(store.data()?.entries).toEqual([]);
      },
    );

    it('devolve o problema sem reler o mes quando o dado e recusado', async () => {
      const { store, http } = setup();
      const target = entry();
      const showing = store.show('2026-11');
      http.expectOne('/api/finance/months/2026-11').flush(monthEntries({ entries: [target] }));
      await showing;

      const saving = store.setAmount(target, 0);
      http
        .expectOne({ method: 'PUT', url: `/api/finance/entries/${target.id}/amount` })
        .flush(...fail(400, 'expense.amount.invalid'));

      expect((await saving)?.code).toBe('expense.amount.invalid');
    });

    it('vira "erro" se reler o mes falhar', async () => {
      const { store, http } = setup();
      const target = entry();
      const showing = store.show('2026-11');
      http.expectOne('/api/finance/months/2026-11').flush(monthEntries({ entries: [target] }));
      await showing;

      const saving = store.setAmount(target, 10);
      http
        .expectOne({ method: 'PUT', url: `/api/finance/entries/${target.id}/amount` })
        .flush(entry());
      await flushPromises();
      http
        .expectOne('/api/finance/months/2026-11')
        .flush(null, { status: 500, statusText: 'Erro' });
      await saving;

      expect(store.status()).toBe('error');
    });
  });
});
