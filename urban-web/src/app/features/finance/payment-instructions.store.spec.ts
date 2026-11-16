import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { NO_INSTRUCTIONS, paymentInstructions } from '../../testing/payment-instructions.fixtures';
import { PaymentInstructionsStore } from './payment-instructions.store';

const URL = '/api/finance/payment-instructions';

function setup(): {
  store: InstanceType<typeof PaymentInstructionsStore>;
  http: HttpTestingController;
} {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), PaymentInstructionsStore],
  });
  return {
    store: TestBed.inject(PaymentInstructionsStore),
    http: TestBed.inject(HttpTestingController),
  };
}

function fail(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

describe('PaymentInstructionsStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('comeca carregando, sem dados', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.instructions()).toBeNull();
  });

  it('busca os dados e fica pronto', async () => {
    const { store, http } = setup();
    const saved = paymentInstructions();

    const loading = store.load();
    http.expectOne(URL).flush(saved);
    await loading;

    expect(store.status()).toBe('ready');
    expect(store.instructions()).toEqual(saved);
  });

  it('fica pronto tambem quando nada foi cadastrado ainda', async () => {
    const { store, http } = setup();
    const empty = NO_INSTRUCTIONS;

    const loading = store.load();
    http.expectOne(URL).flush(empty);
    await loading;

    expect(store.status()).toBe('ready');
    expect(store.instructions()).toEqual(empty);
  });

  it('vai para erro quando a busca falha e tenta de novo com load', async () => {
    const { store, http } = setup();

    const first = store.load();
    http.expectOne(URL).flush(null, { status: 503, statusText: 'Erro' });
    await first;
    expect(store.status()).toBe('error');
    expect(store.instructions()).toBeNull();

    const second = store.load();
    expect(store.status()).toBe('loading');
    http.expectOne(URL).flush(paymentInstructions());
    await second;
    expect(store.status()).toBe('ready');
  });

  describe('save', () => {
    const PAYLOAD = {
      pix: { kind: 'cpf' as const, key: '52998224725' },
      account: null,
    };

    async function loaded(): Promise<ReturnType<typeof setup>> {
      const context = setup();
      const loading = context.store.load();
      context.http.expectOne(URL).flush(paymentInstructions());
      await loading;
      return context;
    }

    it('grava, guarda o que a API devolveu e nao devolve problema', async () => {
      const { store, http } = await loaded();
      const result = paymentInstructions({ pix: PAYLOAD.pix, account: null });

      const saving = store.save(PAYLOAD);
      const request = http.expectOne(URL);
      expect(request.request.method).toBe('PUT');
      expect(request.request.body).toEqual(PAYLOAD);
      request.flush(result);

      expect(await saving).toBeNull();
      expect(store.status()).toBe('ready');
      expect(store.instructions()).toEqual(result);
    });

    it('fica gravando enquanto a API nao responde', async () => {
      const { store, http } = await loaded();

      const saving = store.save(PAYLOAD);
      expect(store.status()).toBe('saving');
      http.expectOne(URL).flush(paymentInstructions());
      await saving;

      expect(store.status()).toBe('ready');
    });

    it('devolve o problema quando a API recusa e mantem o que estava gravado', async () => {
      const { store, http } = await loaded();
      const before = store.instructions();
      const [body, options] = fail(400, 'finance.payment_instructions.pix_key_invalid');

      const saving = store.save(PAYLOAD);
      http.expectOne(URL).flush(body, options);
      const result = await saving;
      await flushPromises();

      expect(result).toMatchObject({
        status: 400,
        code: 'finance.payment_instructions.pix_key_invalid',
      });
      expect(store.status()).toBe('ready');
      expect(store.instructions()).toEqual(before);
    });

    it('devolve o problema de rede sem derrubar a tela', async () => {
      const { store, http } = await loaded();

      const saving = store.save(PAYLOAD);
      http.expectOne(URL).flush(null, { status: 503, statusText: 'Erro' });

      expect(await saving).toMatchObject({ status: 503 });
      expect(store.status()).toBe('ready');
    });
  });
});
