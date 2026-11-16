import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { entry, monthClosing, monthEntries, monthPayments } from '../../testing/finance.fixtures';
import { paymentInstructions } from '../../testing/payment-instructions.fixtures';
import { resident, residentPage } from '../../testing/residents.fixtures';
import { unitsSummary } from '../../testing/units.fixtures';
import { DashboardStore } from './dashboard.store';

const MONTH = '2026-11';
const OPEN_MONTH = monthEntries({ entries: [entry()] });
const CLOSED_MONTH = monthEntries({ entries: [entry()], closing: monthClosing() });

type Http = HttpTestingController;

function setup(): { store: InstanceType<typeof DashboardStore>; http: Http } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), DashboardStore],
  });
  return { store: TestBed.inject(DashboardStore), http: TestBed.inject(HttpTestingController) };
}

function failure(status = 500): [unknown, { status: number; statusText: string }] {
  return [problem(status, 'erro'), { status, statusText: 'Erro' }];
}

interface Answers {
  /** O corpo da resposta, ou 'fail' para um erro 500. */
  month?: unknown;
  units?: unknown;
  residents?: unknown;
  instructions?: unknown;
}

function answer(http: Http, url: string, body: unknown): void {
  const request = http.expectOne((r) => r.url === url);
  if (body === 'fail') {
    request.flush(...failure());
  } else {
    request.flush(body);
  }
}

/** Responde as quatro leituras que saem juntas ao abrir o painel. */
function answerFirstReads(http: Http, answers: Answers = {}): void {
  answer(http, `/api/finance/months/${MONTH}`, answers.month ?? OPEN_MONTH);
  answer(http, '/api/units/summary', answers.units ?? unitsSummary());
  answer(http, '/api/residents', answers.residents ?? residentPage([resident()], 37));
  answer(http, '/api/finance/payment-instructions', answers.instructions ?? paymentInstructions());
}

async function opened(
  store: InstanceType<typeof DashboardStore>,
  http: Http,
  answers: Answers = {},
): Promise<void> {
  const loading = store.load(MONTH);
  answerFirstReads(http, answers);
  await flushPromises();
  if (answers.month === CLOSED_MONTH) {
    answer(http, `/api/finance/months/${MONTH}/payments`, monthPayments());
  }
  await loading;
}

describe('DashboardStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('começa carregando tudo, sem nenhum dado', () => {
    const { store } = setup();

    expect(store.month()).toEqual({ status: 'loading', value: null });
    expect(store.payments()).toEqual({ status: 'loading', value: null });
    expect(store.units()).toEqual({ status: 'loading', value: null });
    expect(store.residents()).toEqual({ status: 'loading', value: null });
    expect(store.instructions()).toEqual({ status: 'loading', value: null });
    expect(store.condominiumStatus()).toBe('loading');
    expect(store.pendingsStatus()).toBe('loading');
  });

  describe('load', () => {
    it('lê o mês, as unidades, os moradores atuais e os dados de pagamento', async () => {
      const { store, http } = setup();

      const loading = store.load(MONTH);
      const residents = http.expectOne((r) => r.url === '/api/residents');
      expect(residents.request.params.get('status')).toBe('active');
      expect(residents.request.params.get('page')).toBe('1');
      expect(residents.request.params.get('pageSize')).toBe('1');
      residents.flush(residentPage([resident()], 37));
      answer(http, `/api/finance/months/${MONTH}`, OPEN_MONTH);
      answer(http, '/api/units/summary', unitsSummary({ active: 40 }));
      answer(http, '/api/finance/payment-instructions', paymentInstructions());
      await loading;

      expect(store.month()).toEqual({ status: 'ready', value: OPEN_MONTH });
      expect(store.units().value?.active).toBe(40);
      expect(store.residents()).toEqual({ status: 'ready', value: 37 });
      expect(store.instructions().status).toBe('ready');
      expect(store.condominiumStatus()).toBe('ready');
    });

    it('com o mês aberto não consulta as cobranças, que ainda não existem', async () => {
      const { store, http } = setup();

      await opened(store, http, { month: OPEN_MONTH });

      expect(store.payments()).toEqual({ status: 'ready', value: { closed: false } });
    });

    it('com o mês fechado lê as cobranças em atraso, de uma em uma página, e o total cobrado e recebido', async () => {
      const { store, http } = setup();

      const loading = store.load(MONTH);
      answerFirstReads(http, { month: CLOSED_MONTH });
      await flushPromises();
      const request = http.expectOne((r) => r.url === `/api/finance/months/${MONTH}/payments`);
      expect(request.request.params.get('status')).toBe('overdue');
      expect(request.request.params.get('page')).toBe('1');
      expect(request.request.params.get('pageSize')).toBe('1');
      request.flush(monthPayments({ charged: 8000, received: 6000, total: 3 }));
      await loading;

      expect(store.payments()).toEqual({
        status: 'ready',
        value: { closed: true, charged: 8000, received: 6000, overdue: 3 },
      });
    });

    it('monta as pendências com o que foi lido', async () => {
      const { store, http } = setup();

      await opened(store, http, {
        month: monthEntries({ entries: [entry()], needsAmountCount: 2 }),
        instructions: paymentInstructions({ pix: null, account: null }),
      });

      expect(store.pendingsStatus()).toBe('ready');
      expect(store.pendings().map((item) => item.id)).toEqual([
        'apportionment',
        'payment-instructions',
        'amounts-pending',
      ]);
    });
  });

  describe('quando uma leitura falha', () => {
    it('o mês falha junto com as cobranças, e o resto segue', async () => {
      const { store, http } = setup();

      await opened(store, http, { month: 'fail' });

      expect(store.month().status).toBe('error');
      expect(store.payments().status).toBe('error');
      expect(store.units().status).toBe('ready');
      expect(store.residents().status).toBe('ready');
      expect(store.condominiumStatus()).toBe('ready');
      expect(store.pendingsStatus()).toBe('error');
    });

    it('as unidades falham e o cartão do condomínio fica com erro', async () => {
      const { store, http } = setup();

      await opened(store, http, { units: 'fail' });

      expect(store.units().status).toBe('error');
      expect(store.month().status).toBe('ready');
      expect(store.condominiumStatus()).toBe('error');
    });

    it('os moradores falham e o cartão do condomínio fica com erro', async () => {
      const { store, http } = setup();

      await opened(store, http, { residents: 'fail' });

      expect(store.residents().status).toBe('error');
      expect(store.condominiumStatus()).toBe('error');
    });

    it('as cobranças falham e o mês continua pronto', async () => {
      const { store, http } = setup();

      const loading = store.load(MONTH);
      answerFirstReads(http, { month: CLOSED_MONTH });
      await flushPromises();
      http.expectOne((r) => r.url === `/api/finance/months/${MONTH}/payments`).flush(...failure());
      await loading;

      expect(store.month().status).toBe('ready');
      expect(store.payments().status).toBe('error');
    });

    it('não aponta pendência do que não conseguiu ler, e avisa que a lista está incompleta', async () => {
      const { store, http } = setup();

      await opened(store, http, { instructions: 'fail' });

      expect(store.pendings().map((item) => item.id)).not.toContain('payment-instructions');
      expect(store.pendingsStatus()).toBe('error');
    });
  });

  describe('tentar de novo', () => {
    it('lê só o mês e as cobranças quando é o cartão do mês que falhou', async () => {
      const { store, http } = setup();
      await opened(store, http, { month: 'fail' });

      const retrying = store.retryMonth();
      expect(store.month().status).toBe('loading');
      answer(http, `/api/finance/months/${MONTH}`, OPEN_MONTH);
      await retrying;

      expect(store.month()).toEqual({ status: 'ready', value: OPEN_MONTH });
      expect(store.payments()).toEqual({ status: 'ready', value: { closed: false } });
    });

    it('lê só as unidades e os moradores quando é o cartão do condomínio que falhou', async () => {
      const { store, http } = setup();
      await opened(store, http, { units: 'fail' });

      const retrying = store.retryCondominium();
      expect(store.units().status).toBe('loading');
      answer(http, '/api/units/summary', unitsSummary());
      answer(http, '/api/residents', residentPage([resident()], 37));
      await retrying;

      expect(store.condominiumStatus()).toBe('ready');
    });

    it('refaz só o que falhou ao tentar de novo as pendências', async () => {
      const { store, http } = setup();
      await opened(store, http, { instructions: 'fail' });

      const retrying = store.retryFailed();
      answer(http, '/api/finance/payment-instructions', paymentInstructions());
      await retrying;

      expect(store.instructions().status).toBe('ready');
      expect(store.pendingsStatus()).toBe('ready');
    });
  });
});
