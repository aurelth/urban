import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { Router, provideRouter } from '@angular/router';
import { SessionStore } from '../../core/auth/session.store';
import { NOW } from '../../core/finance/clock';
import { formatMoney } from '../../core/finance/money';
import { MonthPayments } from '../../core/finance/payments.models';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, problem, signInAs } from '../../testing/auth.fixtures';
import { submitDialog, typeIn } from '../../testing/dialog.harness';
import {
  chargePayments,
  chargeSummary,
  monthPayments,
  payment,
} from '../../testing/finance.fixtures';
import { chooseMenuOption, menuSelectOptions, menuSelectValue } from '../../testing/menu-select';
import { FileSaver } from '../../shared/files/file-saver';
import { PaymentsPage } from './payments.page';

/** formatMoney usa espaço sem quebra entre "R$" e o valor; o texto lido da tela é normalizado para espaço comum. */
function money(amount: number): string {
  return formatMoney(amount).replace(/\s/g, ' ');
}

const LIST = (month: string): string => `/api/finance/months/${month}/payments`;

const OVERDUE = chargeSummary({
  chargeId: 'c1',
  unitId: 'u1',
  block: 'A',
  number: '101',
  amount: 800,
  paid: 0,
  balance: 800,
  status: 'overdue',
});
const PARTIAL = chargeSummary({
  chargeId: 'c2',
  unitId: 'u2',
  block: 'A',
  number: '102',
  amount: 800,
  paid: 500,
  balance: 300,
  status: 'partial',
  lastReceivedOn: '2026-11-10',
});
const PAID = chargeSummary({
  chargeId: 'c3',
  unitId: 'u3',
  block: null,
  number: '201',
  amount: 800,
  paid: 800,
  balance: 0,
  status: 'paid',
  lastReceivedOn: '2026-11-12',
});

const MONTH = monthPayments({
  charged: 2400,
  received: 1300,
  total: 3,
  items: [OVERDUE, PARTIAL, PAID],
});

interface Harness {
  fixture: ComponentFixture<PaymentsPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

async function setup(
  data: MonthPayments | 'pending' | 'not-closed' = MONTH,
  query = '',
  month = '2026-11',
): Promise<Harness> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      { provide: NOW, useValue: () => new Date('2026-11-15T15:00:00Z') },
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  await signInAs(TestBed.inject(SessionStore), http, REGISTERED_MANAGER);
  if (query !== '') {
    await TestBed.inject(Router).navigateByUrl(`/${query}`);
  }
  const fixture = TestBed.createComponent(PaymentsPage);
  fixture.detectChanges();
  if (data === 'not-closed') {
    listRequest(http, month).flush(problem(409, 'closing.not_closed'), {
      status: 409,
      statusText: 'Erro',
    });
    await settle(fixture);
  } else if (data !== 'pending') {
    listRequest(http, month).flush(data);
    await settle(fixture);
  }
  return { fixture, http, html: fixture.nativeElement };
}

function listRequest(http: HttpTestingController, month = '2026-11'): TestRequest {
  return http.expectOne((request) => request.url === LIST(month));
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    await flushPromises();
    TestBed.tick();
    fixture.detectChanges();
    await fixture.whenStable();
  }
}

function testId(html: ParentNode, id: string): HTMLElement | null {
  return html.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function plain(value: string | null | undefined): string {
  return (value ?? '').replace(/\s+/g, ' ').replaceAll(' ', ' ').trim();
}

function rows(html: HTMLElement): HTMLElement[] {
  return [...html.querySelectorAll<HTMLElement>('[data-testid="charge-row"]')];
}

/** O nome do ícone do Material dentro do elemento (o `mat-icon` põe o nome em `data-mat-icon-name`). */
function iconOf(element: HTMLElement | null): string | null | undefined {
  return element?.querySelector('mat-icon')?.getAttribute('data-mat-icon-name');
}

function click(element: HTMLElement | null): void {
  element!.click();
}

describe('PaymentsPage', () => {
  afterEach(() => {
    document.querySelectorAll('.cdk-overlay-container').forEach((overlay) => overlay.remove());
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  describe('carregamento', () => {
    it('mostra que esta carregando', async () => {
      const { html, http } = await setup('pending');

      expect(testId(html, 'loading')).not.toBeNull();
      listRequest(http);
    });

    it('mostra o erro e tenta de novo', async () => {
      const { fixture, html, http } = await setup('pending');
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await settle(fixture);

      expect(testId(html, 'load-error')).not.toBeNull();

      click(testId(html, 'retry'));
      listRequest(http).flush(MONTH);
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(rows(html)).toHaveLength(3);
    });

    it('explica que o mes ainda nao foi fechado e leva aos lancamentos do mes', async () => {
      const { html } = await setup('not-closed');

      expect(plain(testId(html, 'not-closed')?.textContent)).toContain(
        'Novembro de 2026 ainda não foi fechado',
      );
      expect(testId(html, 'payments-table')).toBeNull();
      expect(testId(html, 'close-link')?.getAttribute('href')).toBe(
        '/financeiro/lancamentos?mes=2026-11',
      );
    });

    it('abre no mes de hoje, com o titulo do mes', async () => {
      const { html } = await setup();

      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Novembro de 2026');
    });

    it('abre no mes pedido na barra de enderecos, quando cabe na janela de 5 anos', async () => {
      const { html } = await setup(monthPayments({ month: '2026-09' }), '?mes=2026-09', '2026-09');

      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Setembro de 2026');
    });

    it.each(['2026-13', 'abc', '2040-01'])(
      'ignora o mes pedido %s e abre no mes de hoje',
      async (query) => {
        const { html } = await setup(MONTH, `?mes=${query}`);

        expect(plain(testId(html, 'month-title')?.textContent)).toBe('Novembro de 2026');
      },
    );

    it('aponta para os lancamentos do mes', async () => {
      const { html } = await setup();

      expect(testId(html, 'entries-link')?.getAttribute('href')).toBe('/financeiro/lancamentos');
    });
  });

  describe('resumo e cobrancas', () => {
    it('mostra o cobrado, o recebido, o que falta receber e o vencimento', async () => {
      const { html } = await setup();

      expect(plain(testId(html, 'stat-charged')?.textContent)).toContain(money(2400));
      expect(plain(testId(html, 'stat-received')?.textContent)).toContain(money(1300));
      expect(plain(testId(html, 'stat-to-receive')?.textContent)).toContain(money(1100));
      expect(plain(testId(html, 'due-date')?.textContent)).toBe('Vencimento em 10/12/2026');
    });

    it('lista uma cobranca por unidade, com valor, pago, saldo e situacao', async () => {
      const { html } = await setup();

      const [first, second, third] = rows(html);
      expect(plain(testId(first, 'unit')?.textContent)).toBe('Bloco A · 101');
      expect(plain(testId(first, 'amount')?.textContent)).toBe(money(800));
      expect(plain(testId(second, 'paid')?.textContent)).toContain(money(500));
      expect(plain(testId(second, 'paid')?.textContent)).toContain('Último em 10/11/2026');
      expect(plain(testId(second, 'balance')?.textContent)).toBe(money(300));
      expect(plain(testId(third, 'unit')?.textContent)).toBe('201');
      expect(plain(testId(third, 'balance')?.textContent)).toBe(money(0));
    });

    it('mostra a situacao com texto, icone e cor de estado', async () => {
      const { html } = await setup();

      const statuses = rows(html).map((row) => testId(row, 'status')!);
      expect(statuses.map((status) => plain(status.textContent))).toEqual([
        'Atrasada',
        'Parcial',
        'Paga',
      ]);
      expect(statuses.map((status) => status.className)).toEqual([
        expect.stringContaining('text-error'),
        expect.stringContaining('text-warning'),
        expect.stringContaining('text-success'),
      ]);
      expect(statuses.every((status) => status.querySelector('mat-icon') !== null)).toBe(true);
    });

    it('so oferece registrar pagamento onde ainda ha saldo, e ver pagamentos em todas', async () => {
      const { html } = await setup();

      const [first, second, third] = rows(html);
      expect(testId(first, 'register-payment')).not.toBeNull();
      expect(testId(second, 'register-payment')).not.toBeNull();
      expect(testId(third, 'register-payment')).toBeNull();
      expect(rows(html).every((row) => testId(row, 'show-payments') !== null)).toBe(true);
      expect(testId(first, 'register-payment')!.getAttribute('aria-label')).toBe(
        'Registrar pagamento de Bloco A · 101',
      );
      expect(testId(third, 'show-payments')!.getAttribute('aria-label')).toBe(
        'Ver pagamentos de 201',
      );
    });

    it('explica quando nenhuma cobranca passa pelo filtro', async () => {
      const { html } = await setup(monthPayments({ items: [], total: 0 }));

      expect(testId(html, 'empty')).not.toBeNull();
      expect(testId(html, 'pagination')).toBeNull();
    });
  });

  describe('meses, filtro e paginas', () => {
    it('troca de mes e le o novo mes', async () => {
      const { fixture, html, http } = await setup();

      click(html.querySelector('button[aria-label="Próximo mês"]'));
      listRequest(http, '2026-12').flush(monthPayments({ month: '2026-12' }));
      await settle(fixture);

      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Dezembro de 2026');
    });

    it('oferece as situacoes e busca pela escolhida, na primeira pagina', async () => {
      const { fixture, html, http } = await setup();
      const filter = testId(html, 'filter-status')!;

      expect(menuSelectOptions(filter).map((option) => option.label)).toEqual([
        'Todas as situações',
        'Em aberto',
        'Parciais',
        'Pagas',
        'Atrasadas',
      ]);
      expect(menuSelectValue(filter)).toBe('all');

      chooseMenuOption(filter, 'overdue');
      const request = listRequest(http);
      expect(request.request.params.get('status')).toBe('overdue');
      request.flush(monthPayments({ items: [OVERDUE], total: 1 }));
      await settle(fixture);

      expect(rows(html)).toHaveLength(1);
      expect(testId(html, 'clear-filter')).not.toBeNull();
    });

    it('limpa o filtro', async () => {
      const { fixture, html, http } = await setup();
      chooseMenuOption(testId(html, 'filter-status')!, 'paid');
      listRequest(http).flush(monthPayments({ items: [PAID], total: 1 }));
      await settle(fixture);

      click(testId(html, 'clear-filter'));
      const request = listRequest(http);
      expect(request.request.params.has('status')).toBe(false);
      request.flush(MONTH);
      await settle(fixture);

      expect(rows(html)).toHaveLength(3);
      expect(testId(html, 'clear-filter')).toBeNull();
    });

    it('pagina pelo total de cobrancas do filtro', async () => {
      const { fixture, html, http } = await setup(monthPayments({ total: 120 }));

      expect(plain(testId(html, 'page-status')?.textContent)).toBe('Página 1 de 3');
      expect((testId(html, 'previous') as HTMLButtonElement).disabled).toBe(true);

      click(testId(html, 'next'));
      const request = listRequest(http);
      expect(request.request.params.get('page')).toBe('2');
      request.flush(monthPayments({ page: 2, total: 120 }));
      await settle(fixture);

      expect(plain(testId(html, 'page-status')?.textContent)).toBe('Página 2 de 3');
    });
  });

  describe('registrar pagamento', () => {
    async function openRegister(): Promise<Harness> {
      const harness = await setup();
      click(testId(rows(harness.html)[1], 'register-payment'));
      await settle(harness.fixture);
      return harness;
    }

    it('abre o dialogo com o saldo da unidade', async () => {
      await openRegister();

      const dialog = document.querySelector<HTMLElement>('urban-register-payment-dialog')!;
      expect(dialog.querySelector('h2')?.textContent?.trim()).toBe(
        'Registrar pagamento de Bloco A · 102',
      );
      expect((dialog.querySelector('[formControlName="amount"]') as HTMLInputElement).value).toBe(
        '300,00',
      );
    });

    it('grava, le o mes de novo e avisa com o valor e a unidade', async () => {
      const { fixture, html, http } = await openRegister();

      typeIn('urban-register-payment-dialog', 'amount', '100');
      submitDialog('urban-register-payment-dialog');
      const post = http.expectOne({ method: 'POST', url: '/api/finance/charges/c2/payments' });
      expect(post.request.body).toEqual({
        amount: 100,
        receivedOn: '2026-11-15',
        method: 'pix',
        note: null,
      });
      post.flush(payment({ chargeId: 'c2', amount: 100 }), { status: 201, statusText: 'Created' });
      await flushPromises();
      listRequest(http).flush(monthPayments({ charged: 2400, received: 1400, items: [PARTIAL] }));
      await settle(fixture);

      expect(document.querySelector('urban-register-payment-dialog')).toBeNull();
      expect(plain(testId(html, 'notice')?.textContent)).toBe(
        `Pagamento de ${money(100)} registrado em Bloco A · 102`,
      );
      expect(plain(testId(html, 'stat-received')?.textContent)).toContain(money(1400));
    });

    it('mostra no dialogo quanto falta quando o saldo mudou em outro lugar', async () => {
      const { fixture, http } = await openRegister();

      submitDialog('urban-register-payment-dialog');
      http
        .expectOne({ method: 'POST', url: '/api/finance/charges/c2/payments' })
        .flush(problem(422, 'finance.payment.exceeds_balance', 'x', { balance: 120 }), {
          status: 422,
          statusText: 'Erro',
        });
      await flushPromises();
      listRequest(http).flush(MONTH);
      await settle(fixture);

      const dialog = document.querySelector<HTMLElement>('urban-register-payment-dialog')!;
      expect(plain(dialog.querySelector('[role="alert"]')?.textContent)).toBe(
        `O valor passa do saldo atual de ${money(120)}. Atualizamos a lista`,
      );
    });

    it('explica quando a cobranca nao existe mais e atualiza a lista', async () => {
      const { fixture, http } = await openRegister();

      submitDialog('urban-register-payment-dialog');
      http
        .expectOne({ method: 'POST', url: '/api/finance/charges/c2/payments' })
        .flush(problem(404, 'finance.charge.not_found'), { status: 404, statusText: 'Erro' });
      await flushPromises();
      listRequest(http).flush(MONTH);
      await settle(fixture);

      const dialog = document.querySelector<HTMLElement>('urban-register-payment-dialog')!;
      expect(plain(dialog.querySelector('[role="alert"]')?.textContent)).toBe(
        'Esta cobrança não existe mais. Atualizamos a lista',
      );
    });
  });

  describe('baixar a fatura', () => {
    const INVOICE = (chargeId: string): string => `/api/finance/charges/${chargeId}/invoice`;
    let save: jest.SpiedFunction<FileSaver['save']>;

    beforeEach(() => {
      save = jest.spyOn(FileSaver.prototype, 'save').mockImplementation(() => undefined);
    });

    afterEach(() => save.mockRestore());

    it('oferece baixar a fatura em todas as cobrancas, com o nome da unidade no rotulo', async () => {
      const { html } = await setup();

      expect(rows(html).every((row) => testId(row, 'download-invoice') !== null)).toBe(true);
      expect(testId(rows(html)[0], 'download-invoice')!.getAttribute('aria-label')).toBe(
        'Baixar fatura de Bloco A · 101',
      );
      expect(iconOf(testId(rows(html)[2], 'download-invoice'))).toBe('download');
      expect(testId(rows(html)[2], 'download-invoice')!.getAttribute('title')).toBe(
        'Baixar fatura em PDF',
      );
    });

    it('baixa o PDF, salva com o nome que a API sugeriu e avisa com a unidade', async () => {
      const { fixture, html, http } = await setup();

      click(testId(rows(html)[1], 'download-invoice'));
      const pdf = new Blob(['%PDF-1.7'], { type: 'application/pdf' });
      http.expectOne(INVOICE('c2')).flush(pdf, {
        headers: { 'Content-Disposition': "attachment; filename*=UTF-8''fatura-2026-11-a-102.pdf" },
      });
      await settle(fixture);

      expect(save).toHaveBeenCalledTimes(1);
      expect(save).toHaveBeenCalledWith(pdf, 'fatura-2026-11-a-102.pdf');
      expect(plain(testId(html, 'notice')?.textContent)).toBe('Fatura de Bloco A · 102 baixada');
      expect(testId(html, 'invoice-error')).toBeNull();
    });

    it('mostra so o icone de download, sem texto no botao', async () => {
      const { html } = await setup();

      const button = testId(rows(html)[0], 'download-invoice')!;
      expect(plain(button.textContent)).toBe('');
      expect(button.querySelector('mat-icon')!.getAttribute('aria-hidden')).toBe('true');
    });

    it('trava os botoes e mostra que esta baixando enquanto o PDF nao chega', async () => {
      const { fixture, html, http } = await setup();

      click(testId(rows(html)[0], 'download-invoice'));
      await settle(fixture);

      const first = testId(rows(html)[0], 'download-invoice') as HTMLButtonElement;
      expect(iconOf(first)).toBe('progress_activity');
      expect(first.getAttribute('aria-busy')).toBe('true');
      expect(first.disabled).toBe(true);
      expect((testId(rows(html)[1], 'download-invoice') as HTMLButtonElement).disabled).toBe(true);
      expect((testId(rows(html)[1], 'register-payment') as HTMLButtonElement).disabled).toBe(false);
      http.expectOne(INVOICE('c1')).flush(new Blob(['%PDF-1.7']));
      await settle(fixture);

      expect((testId(rows(html)[1], 'download-invoice') as HTMLButtonElement).disabled).toBe(false);
      expect(iconOf(testId(rows(html)[0], 'download-invoice'))).toBe('download');
      expect(testId(rows(html)[0], 'download-invoice')!.getAttribute('aria-busy')).toBe('false');
    });

    it('explica quando a cobranca nao existe mais, sem salvar nada', async () => {
      const { fixture, html, http } = await setup();

      click(testId(rows(html)[0], 'download-invoice'));
      http.expectOne(INVOICE('c1')).flush(
        new Blob([JSON.stringify({ status: 404, code: 'finance.charge.not_found' })], {
          type: 'application/problem+json',
        }),
        { status: 404, statusText: 'Not Found' },
      );
      await settle(fixture);

      expect(save).not.toHaveBeenCalled();
      expect(plain(testId(html, 'invoice-error')?.textContent)).toContain(
        'Esta cobrança não existe mais',
      );
      expect(testId(html, 'notice')).toBeNull();
    });

    it('explica o limite de requisicoes e a falta de conexao', async () => {
      const { fixture, html, http } = await setup();

      click(testId(rows(html)[0], 'download-invoice'));
      http.expectOne(INVOICE('c1')).flush(new Blob(['x']), {
        status: 429,
        statusText: 'Too Many Requests',
        headers: { 'Retry-After': '20' },
      });
      await settle(fixture);
      expect(plain(testId(html, 'invoice-error')?.textContent)).toContain('Aguarde 20 segundos');

      click(testId(rows(html)[0], 'download-invoice'));
      http.expectOne(INVOICE('c1')).error(new ProgressEvent('error'));
      await settle(fixture);
      expect(plain(testId(html, 'invoice-error')?.textContent)).toContain(
        'Não foi possível conectar',
      );
    });

    it('troca o aviso de erro pelo de sucesso na proxima tentativa', async () => {
      const { fixture, html, http } = await setup();
      click(testId(rows(html)[0], 'download-invoice'));
      http.expectOne(INVOICE('c1')).flush(new Blob(['x']), { status: 500, statusText: 'Erro' });
      await settle(fixture);
      expect(testId(html, 'invoice-error')).not.toBeNull();

      click(testId(rows(html)[0], 'download-invoice'));
      http.expectOne(INVOICE('c1')).flush(new Blob(['%PDF-1.7']));
      await settle(fixture);

      expect(testId(html, 'invoice-error')).toBeNull();
      expect(testId(html, 'notice')).not.toBeNull();
    });

    it('limpa o aviso de erro ao trocar de mes', async () => {
      const { fixture, html, http } = await setup();
      click(testId(rows(html)[0], 'download-invoice'));
      http.expectOne(INVOICE('c1')).flush(new Blob(['x']), { status: 500, statusText: 'Erro' });
      await settle(fixture);

      click(html.querySelector('button[aria-label="Próximo mês"]'));
      listRequest(http, '2026-12').flush(MONTH);
      await settle(fixture);

      expect(testId(html, 'invoice-error')).toBeNull();
    });
  });

  describe('ver pagamentos e estornar', () => {
    it('abre o historico da cobranca e, sem mudanca, nao busca a lista de novo', async () => {
      const { fixture, html, http } = await setup();

      click(testId(rows(html)[1], 'show-payments'));
      await flushPromises();
      TestBed.tick();
      http
        .expectOne({ method: 'GET', url: '/api/finance/charges/c2/payments' })
        .flush(chargePayments({ chargeId: 'c2' }));
      await settle(fixture);
      expect(document.querySelector('urban-charge-payments-dialog h2')?.textContent?.trim()).toBe(
        'Pagamentos de Bloco A · 102',
      );

      click(document.querySelector<HTMLElement>('[data-testid="close"]'));
      await settle(fixture);

      expect(document.querySelector('urban-charge-payments-dialog')).toBeNull();
      expect(testId(html, 'notice')).toBeNull();
    });

    it('le a lista de novo e avisa quando houve estorno', async () => {
      const { fixture, html, http } = await setup();
      click(testId(rows(html)[1], 'show-payments'));
      await flushPromises();
      TestBed.tick();
      http
        .expectOne({ method: 'GET', url: '/api/finance/charges/c2/payments' })
        .flush(chargePayments({ chargeId: 'c2', payments: [payment({ id: 'p1' })] }));
      await settle(fixture);
      click(document.querySelector<HTMLElement>('[data-testid="reverse-payment"]'));
      await settle(fixture);
      typeIn('urban-charge-payments-dialog', 'reason', 'Erro de digitação');
      submitDialog('urban-charge-payments-dialog');
      http
        .expectOne({ method: 'POST', url: '/api/finance/payments/p1/reverse' })
        .flush(null, { status: 204, statusText: 'No Content' });
      await flushPromises();
      http
        .expectOne({ method: 'GET', url: '/api/finance/charges/c2/payments' })
        .flush(chargePayments({ chargeId: 'c2', payments: [] }));
      await settle(fixture);

      click(document.querySelector<HTMLElement>('[data-testid="close"]'));
      await flushPromises();
      listRequest(http).flush(MONTH);
      await settle(fixture);

      expect(plain(testId(html, 'notice')?.textContent)).toBe('Estorno registrado');
    });
  });
});
