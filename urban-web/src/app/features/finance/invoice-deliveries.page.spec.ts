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
import { MonthInvoiceDeliveries } from '../../core/finance/invoice-deliveries.models';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, problem, signInAs } from '../../testing/auth.fixtures';
import { invoiceDelivery, monthInvoiceDeliveries } from '../../testing/invoice-deliveries.fixtures';
import { InvoiceDeliveriesPage } from './invoice-deliveries.page';

const LIST = (month: string): string => `/api/finance/months/${month}/invoice-deliveries`;
const RESEND = (chargeId: string): string => `/api/finance/charges/${chargeId}/invoice-deliveries`;
const ACCEPTED = { status: 202, statusText: 'Accepted' };

const NOT_SENT = invoiceDelivery({
  chargeId: 'c1',
  unitId: 'u1',
  block: 'A',
  number: '101',
  responsibleName: 'Maria Souza',
  state: 'notSent',
});
const FAILED = invoiceDelivery({
  chargeId: 'c2',
  unitId: 'u2',
  block: 'A',
  number: '102',
  responsibleName: 'João Lima',
  state: 'failed',
  failureReason: 'not_delivered',
});
const QUEUED = invoiceDelivery({
  chargeId: 'c3',
  unitId: 'u3',
  block: 'A',
  number: '103',
  responsibleName: 'Ana Costa',
  state: 'queued',
});
const SENT = invoiceDelivery({
  chargeId: 'c4',
  unitId: 'u4',
  block: null,
  number: '201',
  responsibleName: 'Paulo Reis',
  state: 'sent',
  sentAt: '2026-11-12T13:30:00Z',
});
const NO_RECIPIENT = invoiceDelivery({
  chargeId: 'c5',
  unitId: 'u5',
  block: 'B',
  number: '301',
  responsibleName: 'Carlos Dias',
  state: 'noRecipient',
});

const MONTH = monthInvoiceDeliveries({ items: [NOT_SENT, FAILED, QUEUED, SENT, NO_RECIPIENT] });
/** O mês sem nada na fila, para a tela não ficar atualizando sozinha nos testes que não falam disso. */
const QUIET = monthInvoiceDeliveries({ items: [NOT_SENT, FAILED, SENT, NO_RECIPIENT] });

interface Harness {
  fixture: ComponentFixture<InvoiceDeliveriesPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

interface SetupOptions {
  query?: string;
  month?: string;
  /** Relógio de mentira ligado antes de a tela abrir, para andar no tempo da atualização automática. */
  fakeTimers?: boolean;
}

async function setup(
  data: MonthInvoiceDeliveries | 'pending' | 'not-closed' = QUIET,
  { query = '', month = '2026-11', fakeTimers = false }: SetupOptions = {},
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
  if (fakeTimers) {
    jest.useFakeTimers();
    fakeClock = true;
  }
  const fixture = TestBed.createComponent(InvoiceDeliveriesPage);
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
  return http.expectOne((request) => request.method === 'GET' && request.url === LIST(month));
}

/** Nenhuma leitura da lista (os pedidos dos ícones, que também são GET, não contam). */
function expectNoList(http: HttpTestingController): void {
  http.expectNone((request) => request.method === 'GET' && request.url === LIST('2026-11'));
}

/** Com o relógio de mentira ligado, `flushPromises` (que usa `setTimeout`) não andaria: avança zero milissegundo no lugar. */
let fakeClock = false;

/** Espera as promessas e os ciclos de detecção. */
async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    await (fakeClock ? jest.advanceTimersByTimeAsync(0) : flushPromises());
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
  return [...html.querySelectorAll<HTMLElement>('[data-testid="delivery-row"]')];
}

function rowOf(html: HTMLElement, unit: string): HTMLElement {
  return rows(html).find((row) => plain(testId(row, 'unit')?.textContent) === unit)!;
}

/** O nome do ícone do Material dentro do elemento (o `mat-icon` põe o nome em `data-mat-icon-name`). */
function iconOf(element: HTMLElement | null): string | null | undefined {
  return element?.querySelector('mat-icon')?.getAttribute('data-mat-icon-name');
}

/** O título e o número de um cartão de contagem. */
function card(html: HTMLElement, id: string): string[] {
  const element = testId(html, id);
  return [
    plain(element?.querySelector('dt')?.textContent),
    plain(element?.querySelector('dd')?.textContent),
  ];
}

function click(element: HTMLElement | null): void {
  element!.click();
}

describe('InvoiceDeliveriesPage', () => {
  afterEach(() => {
    jest.useRealTimers();
    fakeClock = false;
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
      listRequest(http).flush(QUIET);
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(rows(html)).toHaveLength(4);
    });

    it('explica que o mes ainda nao foi fechado e leva aos lancamentos do mes', async () => {
      const { html } = await setup('not-closed');

      expect(plain(testId(html, 'not-closed')?.textContent)).toContain(
        'Novembro de 2026 ainda não foi fechado',
      );
      expect(testId(html, 'deliveries-table')).toBeNull();
      expect(testId(html, 'send-month')).toBeNull();
      expect(testId(html, 'close-link')?.getAttribute('href')).toBe(
        '/financeiro/lancamentos?mes=2026-11',
      );
    });

    it('abre no mes de hoje, com o titulo do mes', async () => {
      const { html } = await setup();

      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Novembro de 2026');
    });

    it('abre no mes pedido na barra de enderecos, quando cabe na janela de 5 anos', async () => {
      const { html } = await setup(monthInvoiceDeliveries({ month: '2026-09' }), {
        query: '?mes=2026-09',
        month: '2026-09',
      });

      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Setembro de 2026');
    });

    it.each(['2026-13', 'abc', '2040-01'])(
      'ignora o mes pedido %s e abre no mes de hoje',
      async (query) => {
        const { html } = await setup(QUIET, { query: `?mes=${query}` });

        expect(plain(testId(html, 'month-title')?.textContent)).toBe('Novembro de 2026');
      },
    );

    it('troca de mes, le o novo mes e limpa os avisos do anterior', async () => {
      const { fixture, html, http } = await setup();
      click(testId(rows(html)[0], 'resend'));
      http.expectOne({ method: 'POST', url: RESEND('c1') }).flush(null, ACCEPTED);
      await settle(fixture);
      listRequest(http).flush(QUIET);
      await settle(fixture);
      expect(testId(html, 'notice')).not.toBeNull();

      click(html.querySelector('button[aria-label="Próximo mês"]'));
      listRequest(http, '2026-12').flush(monthInvoiceDeliveries({ month: '2026-12' }));
      await settle(fixture);

      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Dezembro de 2026');
      expect(testId(html, 'notice')).toBeNull();
    });
  });

  describe('resumo do mes', () => {
    it('conta as faturas por situacao, em cartoes com texto', async () => {
      const { html } = await setup(MONTH);

      expect(card(html, 'count-sent')).toEqual(['Enviadas', '1']);
      expect(card(html, 'count-queued')).toEqual(['Na fila', '1']);
      expect(card(html, 'count-failed')).toEqual(['Falharam', '1']);
      expect(card(html, 'count-not-sent')).toEqual(['Não enviadas', '1']);
      expect(card(html, 'count-no-recipient')).toEqual(['Sem e-mail', '1']);
    });

    it('usa as contagens do mes inteiro, nao so as da pagina', async () => {
      const { html } = await setup(
        monthInvoiceDeliveries({
          items: [NOT_SENT],
          total: 120,
          counts: { noRecipient: 2, notSent: 100, queued: 0, sent: 15, failed: 3 },
        }),
      );

      expect(card(html, 'count-not-sent')).toEqual(['Não enviadas', '100']);
      expect(card(html, 'count-sent')).toEqual(['Enviadas', '15']);
    });

    it('diz quantas faturas o envio do mes mandaria: as nao enviadas e as que falharam', async () => {
      const { html } = await setup();

      expect(plain(testId(html, 'pending')?.textContent)).toBe('2 faturas a enviar');
    });

    it('usa o singular para uma fatura', async () => {
      const { html } = await setup(monthInvoiceDeliveries({ items: [NOT_SENT, SENT] }));

      expect(plain(testId(html, 'pending')?.textContent)).toBe('1 fatura a enviar');
    });

    it('avisa quando nao ha nenhuma e nao deixa enviar', async () => {
      const { html } = await setup(monthInvoiceDeliveries({ items: [SENT, NO_RECIPIENT] }));

      expect(plain(testId(html, 'pending')?.textContent)).toBe('Nenhuma fatura a enviar');
      expect((testId(html, 'send-month') as HTMLButtonElement).disabled).toBe(true);
    });
  });

  describe('unidades', () => {
    it('lista uma linha por unidade, com o responsavel', async () => {
      const { html } = await setup();

      expect(rows(html).map((row) => plain(testId(row, 'unit')?.textContent))).toEqual([
        'Bloco A · 101',
        'Bloco A · 102',
        '201',
        'Bloco B · 301',
      ]);
      expect(plain(testId(rows(html)[0], 'responsible')?.textContent)).toBe('Maria Souza');
    });

    it('avisa quando a unidade nao tem responsavel', async () => {
      const { html } = await setup(
        monthInvoiceDeliveries({ items: [{ ...NO_RECIPIENT, responsibleName: null }] }),
      );

      expect(plain(testId(rows(html)[0], 'responsible')?.textContent)).toBe('Sem responsável');
    });

    it('mostra a situacao com texto, icone e cor de estado', async () => {
      const { html } = await setup(MONTH);

      const states = rows(html).map((row) => testId(row, 'state')!);
      expect(states.map((state) => plain(state.textContent))).toEqual([
        'Não enviada',
        'Falhou',
        'Na fila',
        'Enviada',
        'Sem e-mail',
      ]);
      expect(states.map((state) => iconOf(state))).toEqual([
        'mail',
        'error',
        'schedule',
        'check_circle',
        'warning',
      ]);
      expect(states.map((state) => state.className)).toEqual([
        expect.stringContaining('text-on-surface-variant'),
        expect.stringContaining('text-error'),
        expect.stringContaining('text-warning'),
        expect.stringContaining('text-success'),
        expect.stringContaining('text-warning'),
      ]);
    });

    it('conta quando a fatura foi enviada, na hora de Brasilia, e por que falhou', async () => {
      const { html } = await setup();

      expect(plain(testId(rowOf(html, '201'), 'detail')?.textContent)).toBe('12/11/2026 às 10:30');
      expect(plain(testId(rowOf(html, 'Bloco A · 102'), 'detail')?.textContent)).toBe(
        'O provedor de e-mail recusou o envio',
      );
      expect(testId(rowOf(html, 'Bloco A · 101'), 'detail')).toBeNull();
    });

    it('oferece a acao certa para cada situacao, com a unidade no rotulo', async () => {
      const { html } = await setup(MONTH);

      const action = (unit: string): HTMLElement | null => testId(rowOf(html, unit), 'resend');
      expect(plain(action('Bloco A · 101')?.textContent)).toBe('Enviar');
      expect(plain(action('Bloco A · 102')?.textContent)).toBe('Enviar de novo');
      expect(plain(action('201')?.textContent)).toBe('Reenviar');
      expect(action('Bloco A · 103')).toBeNull();
      expect(action('Bloco B · 301')).toBeNull();
      expect(action('Bloco A · 101')!.getAttribute('aria-label')).toBe(
        'Enviar fatura de Bloco A · 101',
      );
      expect(action('201')!.getAttribute('aria-label')).toBe('Reenviar fatura de 201');
    });

    it('leva aos moradores quando falta o e-mail do responsavel', async () => {
      const { html } = await setup(MONTH);

      const link = testId(rowOf(html, 'Bloco B · 301'), 'add-email');
      expect(link?.getAttribute('href')).toBe('/moradores');
      expect(plain(link?.textContent)).toBe('Cadastrar e-mail');
      expect(testId(rowOf(html, '201'), 'add-email')).toBeNull();
    });

    it('explica quando o mes nao tem nenhuma cobranca', async () => {
      const { html } = await setup(monthInvoiceDeliveries({ items: [], total: 0 }));

      expect(testId(html, 'empty')).not.toBeNull();
      expect(testId(html, 'deliveries-table')).toBeNull();
    });
  });

  describe('enviar as faturas do mes', () => {
    it('pede confirmacao antes de mandar, sem chamar a API', async () => {
      const { fixture, html, http } = await setup();

      click(testId(html, 'send-month'));
      await settle(fixture);

      const confirmation = testId(html, 'send-confirm');
      expect(plain(confirmation?.textContent)).toContain('Enviar 2 faturas de Novembro de 2026');
      http.expectNone((request) => request.method === 'POST');
    });

    it('cancela sem mandar nada', async () => {
      const { fixture, html, http } = await setup();
      click(testId(html, 'send-month'));
      await settle(fixture);

      click(testId(html, 'cancel-send'));
      await settle(fixture);

      expect(testId(html, 'send-confirm')).toBeNull();
      http.expectNone((request) => request.method === 'POST');
    });

    it('manda, conta o que aconteceu e le a lista de novo', async () => {
      const { fixture, html, http } = await setup();
      click(testId(html, 'send-month'));
      await settle(fixture);

      click(testId(html, 'confirm-send'));
      const post = http.expectOne({ method: 'POST', url: LIST('2026-11') });
      expect(post.request.body).toBeNull();
      post.flush({ queued: 2, alreadySent: 1, alreadyQueued: 1, withoutRecipient: 1 }, ACCEPTED);
      await settle(fixture);
      listRequest(http).flush(MONTH);
      await settle(fixture);

      expect(testId(html, 'send-confirm')).toBeNull();
      expect(plain(testId(html, 'notice')?.textContent)).toBe(
        '2 faturas entraram na fila de envio. 1 unidade já tinha recebido a fatura. ' +
          '1 fatura já estava na fila. 1 unidade ficou de fora por não ter responsável com e-mail',
      );
      expect(card(html, 'count-queued')).toEqual(['Na fila', '1']);
    });

    it('trava os botoes enquanto manda, para nao mandar duas vezes', async () => {
      const { fixture, html, http } = await setup();
      click(testId(html, 'send-month'));
      await settle(fixture);

      click(testId(html, 'confirm-send'));
      await settle(fixture);

      expect((testId(html, 'confirm-send') as HTMLButtonElement).disabled).toBe(true);
      expect((testId(html, 'cancel-send') as HTMLButtonElement).disabled).toBe(true);
      click(testId(html, 'confirm-send'));
      expect(http.match((request) => request.method === 'POST')).toHaveLength(1);
      expect(
        rows(html).every(
          (row) => (testId(row, 'resend') as HTMLButtonElement | null)?.disabled ?? true,
        ),
      ).toBe(true);
    });

    it('explica o limite de requisicoes e fecha a confirmacao', async () => {
      const { fixture, html, http } = await setup();
      click(testId(html, 'send-month'));
      await settle(fixture);

      click(testId(html, 'confirm-send'));
      http
        .expectOne({ method: 'POST', url: LIST('2026-11') })
        .flush(problem(429, 'rate_limit.exceeded'), {
          status: 429,
          statusText: 'Too Many Requests',
          headers: { 'Retry-After': '20' },
        });
      await settle(fixture);

      expect(plain(testId(html, 'action-error')?.textContent)).toContain('Aguarde 20 segundos');
      expect(testId(html, 'send-confirm')).toBeNull();
      expect(testId(html, 'notice')).toBeNull();
    });

    it('mostra "nao fechado" quando o mes foi reaberto em outro lugar', async () => {
      const { fixture, html, http } = await setup();
      click(testId(html, 'send-month'));
      await settle(fixture);

      click(testId(html, 'confirm-send'));
      http
        .expectOne({ method: 'POST', url: LIST('2026-11') })
        .flush(problem(409, 'closing.not_closed'), { status: 409, statusText: 'Conflict' });
      await settle(fixture);
      listRequest(http).flush(problem(409, 'closing.not_closed'), {
        status: 409,
        statusText: 'Conflict',
      });
      await settle(fixture);

      expect(testId(html, 'not-closed')).not.toBeNull();
    });
  });

  describe('enviar a fatura de uma unidade', () => {
    it('manda direto, sem confirmacao, le a lista de novo e avisa com a unidade', async () => {
      const { fixture, html, http } = await setup();

      click(testId(rowOf(html, 'Bloco A · 101'), 'resend'));
      http.expectOne({ method: 'POST', url: RESEND('c1') }).flush(null, ACCEPTED);
      await settle(fixture);
      listRequest(http).flush(MONTH);
      await settle(fixture);

      expect(plain(testId(html, 'notice')?.textContent)).toBe(
        'Fatura de Bloco A · 101 entrou na fila de envio',
      );
      expect(testId(rowOf(html, 'Bloco A · 101'), 'state')).not.toBeNull();
    });

    it('trava as acoes enquanto manda', async () => {
      const { fixture, html, http } = await setup();

      click(testId(rowOf(html, 'Bloco A · 101'), 'resend'));
      await settle(fixture);

      expect((testId(rowOf(html, '201'), 'resend') as HTMLButtonElement).disabled).toBe(true);
      expect((testId(html, 'send-month') as HTMLButtonElement).disabled).toBe(true);
      http.expectOne({ method: 'POST', url: RESEND('c1') });
    });

    it.each([
      [
        422,
        'finance.invoice_delivery.no_recipient',
        'Esta unidade não tem responsável com e-mail cadastrado. Cadastre o e-mail e tente de novo',
      ],
      [
        409,
        'finance.invoice_delivery.already_queued',
        'A fatura desta unidade já está na fila de envio. Atualizamos a lista',
      ],
      [404, 'finance.charge.not_found', 'Esta cobrança não existe mais. Atualizamos a lista'],
    ])('explica a recusa %s (%s) e atualiza a lista', async (status, code, message) => {
      const { fixture, html, http } = await setup();

      click(testId(rowOf(html, 'Bloco A · 101'), 'resend'));
      http
        .expectOne({ method: 'POST', url: RESEND('c1') })
        .flush(problem(status, code), { status, statusText: 'Erro' });
      await settle(fixture);
      listRequest(http).flush(QUIET);
      await settle(fixture);

      expect(plain(testId(html, 'action-error')?.textContent)).toBe(message);
      expect(testId(html, 'notice')).toBeNull();
    });

    it('troca o aviso de erro pelo de sucesso na proxima tentativa', async () => {
      const { fixture, html, http } = await setup();
      click(testId(rowOf(html, 'Bloco A · 101'), 'resend'));
      http
        .expectOne({ method: 'POST', url: RESEND('c1') })
        .flush(problem(429, 'rate_limit.exceeded'), { status: 429, statusText: 'Erro' });
      await settle(fixture);
      expect(testId(html, 'action-error')).not.toBeNull();

      click(testId(rowOf(html, 'Bloco A · 101'), 'resend'));
      http.expectOne({ method: 'POST', url: RESEND('c1') }).flush(null, ACCEPTED);
      await settle(fixture);
      listRequest(http).flush(QUIET);
      await settle(fixture);

      expect(testId(html, 'action-error')).toBeNull();
      expect(testId(html, 'notice')).not.toBeNull();
    });
  });

  describe('atualizacao automatica', () => {
    it('le a lista de novo a cada 5 segundos enquanto houver fatura na fila', async () => {
      const { fixture, html, http } = await setup(MONTH, { fakeTimers: true });

      await jest.advanceTimersByTimeAsync(4_999);
      expectNoList(http);

      await jest.advanceTimersByTimeAsync(1);
      listRequest(http).flush(MONTH);
      await settle(fixture);
      await jest.advanceTimersByTimeAsync(5_000);
      listRequest(http).flush(
        monthInvoiceDeliveries({ items: [NOT_SENT, { ...QUEUED, state: 'sent' }] }),
      );
      await settle(fixture);

      expect(card(html, 'count-queued')).toEqual(['Na fila', '0']);
      expect(card(html, 'count-sent')).toEqual(['Enviadas', '1']);
    });

    it('para quando nada mais esta na fila', async () => {
      const { fixture, http } = await setup(MONTH, { fakeTimers: true });
      await jest.advanceTimersByTimeAsync(5_000);
      listRequest(http).flush(QUIET);
      await settle(fixture);

      await jest.advanceTimersByTimeAsync(30_000);

      expectNoList(http);
    });

    it('nao le a lista sozinha quando nada esta na fila', async () => {
      const { http } = await setup(QUIET, { fakeTimers: true });

      await jest.advanceTimersByTimeAsync(30_000);

      expectNoList(http);
    });

    it('continua tentando quando uma leitura falha', async () => {
      const { fixture, html, http } = await setup(MONTH, { fakeTimers: true });
      await jest.advanceTimersByTimeAsync(5_000);
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      await jest.advanceTimersByTimeAsync(5_000);
      listRequest(http).flush(MONTH);
    });

    it('para quando a tela e fechada', async () => {
      const { fixture, http } = await setup(MONTH, { fakeTimers: true });

      fixture.destroy();
      await jest.advanceTimersByTimeAsync(30_000);

      expectNoList(http);
    });
  });

  describe('paginas', () => {
    it('pagina pelo total de cobrancas do mes', async () => {
      const { fixture, html, http } = await setup(monthInvoiceDeliveries({ total: 120 }));

      expect(plain(testId(html, 'page-status')?.textContent)).toBe('Página 1 de 3');
      expect((testId(html, 'previous') as HTMLButtonElement).disabled).toBe(true);

      click(testId(html, 'next'));
      const request = listRequest(http);
      expect(request.request.params.get('page')).toBe('2');
      request.flush(monthInvoiceDeliveries({ page: 2, total: 120 }));
      await settle(fixture);

      expect(plain(testId(html, 'page-status')?.textContent)).toBe('Página 2 de 3');
    });

    it('nao mostra a paginacao quando cabe numa pagina so', async () => {
      const { html } = await setup();

      expect(testId(html, 'pagination')).toBeNull();
    });
  });
});
