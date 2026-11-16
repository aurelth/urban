import { HttpTestingController } from '@angular/common/http/testing';
import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { formatMoney } from '../../core/finance/money';
import { ChargePayments } from '../../core/finance/payments.models';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import {
  configureDialogTest,
  dialogOf,
  errorsOf,
  removeOverlays,
  submitDialog,
  typeIn,
} from '../../testing/dialog.harness';
import { chargePayments, payment } from '../../testing/finance.fixtures';
import { openChargePaymentsDialog } from './charge-payments.dialog';

/** formatMoney usa espaço sem quebra entre "R$" e o valor; o texto lido da tela é normalizado para espaço comum. */
function money(amount: number): string {
  return formatMoney(amount).replace(/\s/g, ' ');
}

const TAG = 'urban-charge-payments-dialog';
const URL = '/api/finance/charges/c1/payments';
const REVERSE = '/api/finance/payments/p1/reverse';

function testId(id: string): HTMLElement | null {
  return dialogOf(TAG).querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function texts(id: string): string[] {
  return [...dialogOf(TAG).querySelectorAll(`[data-testid="${id}"]`)].map((element) =>
    (element.textContent ?? '').replace(/\s+/g, ' ').replaceAll(' ', ' ').trim(),
  );
}

function summary(id: string): string[] {
  return [...testId(id)!.querySelectorAll('dt, dd')].map((element) =>
    (element.textContent ?? '').replace(/\s+/g, ' ').trim(),
  );
}

async function settle(): Promise<void> {
  for (let round = 0; round < 3; round++) {
    await flushPromises();
    TestBed.tick();
  }
}

async function open(
  data: ChargePayments | 'pending' = chargePayments({ chargeId: 'c1' }),
): Promise<{ http: HttpTestingController; result: Promise<boolean> }> {
  configureDialogTest();
  const result = openChargePaymentsDialog(TestBed.inject(Injector), {
    chargeId: 'c1',
    label: 'Bloco A · 101',
  });
  TestBed.tick();
  const http = TestBed.inject(HttpTestingController);
  if (data !== 'pending') {
    http.expectOne({ method: 'GET', url: URL }).flush(data);
    await settle();
  }
  return { http, result };
}

const TWO = chargePayments({
  chargeId: 'c1',
  amount: 800,
  paid: 500,
  balance: 300,
  status: 'partial',
  payments: [
    payment({ id: 'p2', amount: 200, receivedOn: '2026-11-12', method: 'cash', note: 'No caixa' }),
    payment({
      id: 'p1',
      amount: 500,
      receivedOn: '2026-11-10',
      method: 'pix',
      reversedAt: null,
    }),
    payment({
      id: 'p0',
      amount: 100,
      receivedOn: '2026-11-08',
      method: 'boleto',
      reversedAt: '2026-11-09T14:30:00Z',
      reversalReason: 'Lançado na unidade errada',
    }),
  ],
});

describe('ChargePaymentsDialog', () => {
  afterEach(() => {
    removeOverlays();
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  it('abre carregando e mostra o titulo com a unidade', async () => {
    await open('pending');

    expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe(
      'Pagamentos de Bloco A · 101',
    );
    expect(testId('loading')?.textContent).toContain('Carregando os pagamentos');
    TestBed.inject(HttpTestingController).expectOne({ method: 'GET', url: URL }).flush(TWO);
  });

  it('resume a cobranca: valor, pago, saldo e situacao com texto e icone', async () => {
    await open(TWO);

    expect(summary('summary-amount')).toEqual(['Valor', money(800)]);
    expect(summary('summary-paid')).toEqual(['Pago', money(500)]);
    expect(summary('summary-balance')).toEqual(['Saldo', money(300)]);
    expect(texts('summary-status')).toEqual(['Parcial']);
    expect(testId('summary-status')!.querySelector('mat-icon')).not.toBeNull();
  });

  it('lista os pagamentos como a API devolve, com data, forma e observacao', async () => {
    await open(TWO);

    expect(texts('payment-amount')).toEqual([money(200), money(500), money(100)]);
    expect(texts('payment-info')).toEqual([
      '12/11/2026 · Dinheiro · No caixa',
      '10/11/2026 · Pix',
      '08/11/2026 · Boleto',
    ]);
  });

  it('mostra o estorno com data, motivo e sem botao para estornar de novo', async () => {
    await open(TWO);

    const items = dialogOf(TAG).querySelectorAll('[data-testid="payment-item"]');
    expect(items[2].querySelector('[data-testid="reversed"]')?.textContent).toContain(
      'Estornado em 09/11/2026 às 11:30',
    );
    expect(items[2].querySelector('[data-testid="reversal-reason"]')?.textContent).toContain(
      'Lançado na unidade errada',
    );
    expect(items[2].querySelector('[data-testid="reverse-payment"]')).toBeNull();
    expect(items[0].querySelector('[data-testid="reverse-payment"]')).not.toBeNull();
  });

  it('explica quando ainda nao ha pagamentos', async () => {
    await open(chargePayments({ chargeId: 'c1', paid: 0, balance: 800, payments: [] }));

    expect(testId('empty')?.textContent).toContain('Nenhum pagamento registrado');
  });

  it('oferece tentar de novo quando a leitura falha', async () => {
    const { http } = await open('pending');
    http.expectOne({ method: 'GET', url: URL }).flush(null, { status: 500, statusText: 'Erro' });
    await settle();

    expect(testId('load-error')?.textContent).toContain('Não foi possível carregar os pagamentos');
    testId('retry')!.click();
    http.expectOne({ method: 'GET', url: URL }).flush(TWO);
    await settle();

    expect(testId('load-error')).toBeNull();
    expect(texts('payment-amount')).toHaveLength(3);
  });

  it('explica quando a cobranca nao existe mais', async () => {
    const { http } = await open('pending');
    http
      .expectOne({ method: 'GET', url: URL })
      .flush(problem(404, 'finance.charge.not_found'), { status: 404, statusText: 'Erro' });
    await settle();

    expect(testId('not-found')?.textContent).toContain('Esta cobrança não existe mais');
  });

  describe('estorno', () => {
    async function openReverseForm(): Promise<{
      http: HttpTestingController;
      result: Promise<boolean>;
    }> {
      const harness = await open(TWO);
      dialogOf(TAG)
        .querySelectorAll<HTMLButtonElement>('[data-testid="reverse-payment"]')[1]
        .click();
      await settle();
      return harness;
    }

    it('abre o formulario do motivo no proprio pagamento, um por vez', async () => {
      await openReverseForm();

      const items = dialogOf(TAG).querySelectorAll('[data-testid="payment-item"]');
      expect(items[1].querySelector('[data-testid="reverse-form"]')).not.toBeNull();
      expect(dialogOf(TAG).querySelectorAll('[data-testid="reverse-form"]')).toHaveLength(1);

      dialogOf(TAG)
        .querySelectorAll<HTMLButtonElement>('[data-testid="reverse-payment"]')[0]
        .click();
      await settle();

      expect(items[1].querySelector('[data-testid="reverse-form"]')).toBeNull();
      expect(items[0].querySelector('[data-testid="reverse-form"]')).not.toBeNull();
    });

    it('cancelar fecha o formulario sem chamar a API', async () => {
      const { http } = await openReverseForm();

      testId('cancel-reverse')!.click();
      await settle();

      expect(testId('reverse-form')).toBeNull();
      http.expectNone(REVERSE);
    });

    it('exige o motivo', async () => {
      const { http } = await openReverseForm();

      submitDialog(TAG);
      await settle();

      expect(errorsOf(TAG)).toEqual(['Informe o motivo do estorno']);
      http.expectNone(REVERSE);
    });

    it('recusa motivo com mais de 200 caracteres', async () => {
      const { http } = await openReverseForm();

      typeIn(TAG, 'reason', 'a'.repeat(201));
      submitDialog(TAG);
      await settle();

      expect(errorsOf(TAG)).toEqual(['O motivo pode ter até 200 caracteres']);
      http.expectNone(REVERSE);
    });

    it('estorna com o motivo aparado, le a cobranca de novo e avisa', async () => {
      const { http } = await openReverseForm();

      typeIn(TAG, 'reason', '  Valor lançado na unidade errada ');
      submitDialog(TAG);
      const reverse = http.expectOne({ method: 'POST', url: REVERSE });
      expect(reverse.request.body).toEqual({ reason: 'Valor lançado na unidade errada' });
      reverse.flush(null, { status: 204, statusText: 'No Content' });
      await flushPromises();
      http.expectOne({ method: 'GET', url: URL }).flush(
        chargePayments({
          chargeId: 'c1',
          paid: 0,
          balance: 800,
          status: 'open',
          payments: [
            payment({ id: 'p1', reversedAt: '2026-11-15T15:00:00Z', reversalReason: 'x' }),
          ],
        }),
      );
      await settle();

      expect(testId('reverse-form')).toBeNull();
      expect(testId('reverse-notice')?.textContent).toContain('Pagamento estornado');
      expect(texts('summary-status')).toEqual(['Em aberto']);
    });

    it('mostra a recusa da API dentro do formulario, sem apagar o motivo', async () => {
      const { http } = await openReverseForm();

      typeIn(TAG, 'reason', 'Erro de digitação');
      submitDialog(TAG);
      http
        .expectOne({ method: 'POST', url: REVERSE })
        .flush(problem(400, 'finance.payment.reason_invalid'), {
          status: 400,
          statusText: 'Erro',
        });
      await settle();

      expect(testId('reverse-error')?.textContent).toContain('Informe o motivo do estorno');
      expect(
        (dialogOf(TAG).querySelector('[formControlName="reason"]') as HTMLInputElement).value,
      ).toBe('Erro de digitação');
    });

    it('le a cobranca de novo quando o pagamento ja tinha sido estornado em outro lugar', async () => {
      const { http } = await openReverseForm();

      typeIn(TAG, 'reason', 'Erro de digitação');
      submitDialog(TAG);
      http
        .expectOne({ method: 'POST', url: REVERSE })
        .flush(problem(409, 'finance.payment.already_reversed'), {
          status: 409,
          statusText: 'Erro',
        });
      await flushPromises();
      http.expectOne({ method: 'GET', url: URL }).flush(TWO);
      await settle();

      expect(testId('reverse-error')?.textContent).toContain('Este pagamento já foi estornado');
    });
  });

  describe('fechar', () => {
    it('responde falso quando nada mudou', async () => {
      const { result } = await open(TWO);

      testId('close')!.click();

      expect(await result).toBe(false);
    });

    it('responde verdadeiro depois de um estorno, para a lista do mes ser atualizada', async () => {
      const { http, result } = await open(TWO);
      dialogOf(TAG)
        .querySelectorAll<HTMLButtonElement>('[data-testid="reverse-payment"]')[1]
        .click();
      await settle();
      typeIn(TAG, 'reason', 'Erro de digitação');
      submitDialog(TAG);
      http.expectOne({ method: 'POST', url: REVERSE }).flush(null, {
        status: 204,
        statusText: 'No Content',
      });
      await flushPromises();
      http.expectOne({ method: 'GET', url: URL }).flush(TWO);
      await settle();

      testId('close')!.click();

      expect(await result).toBe(true);
    });
  });
});
