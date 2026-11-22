import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { CurrentUser } from '../../core/auth/auth.models';
import { SessionStore } from '../../core/auth/session.store';
import { NOW } from '../../core/finance/clock';
import { MonthEntries } from '../../core/finance/finance.models';
import { formatMoney } from '../../core/finance/money';
import { MonthPayments } from '../../core/finance/payments.models';
import { PaymentInstructions } from '../../core/finance/payment-instructions.models';
import { UnitsSummary } from '../../core/units/units.models';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, problem, signInAs } from '../../testing/auth.fixtures';
import { entry, monthClosing, monthEntries, monthPayments } from '../../testing/finance.fixtures';
import { paymentInstructions } from '../../testing/payment-instructions.fixtures';
import { resident, residentPage } from '../../testing/residents.fixtures';
import { unitsSummary } from '../../testing/units.fixtures';
import { DashboardPage } from './dashboard.page';

/** formatMoney usa espaço sem quebra entre "R$" e o valor; o texto lido da tela é normalizado para espaço comum. */
function money(amount: number): string {
  return formatMoney(amount).replace(/\s/g, ' ');
}

const RESIDENT_USER: CurrentUser = { ...REGISTERED_MANAGER, role: 'Resident' };
const OPEN_MONTH = monthEntries({ entries: [entry()], total: 4200 });
const CLOSED_MONTH = monthEntries({
  entries: [entry()],
  total: 4200,
  closing: monthClosing({ dueDate: '2026-12-10' }),
});
const PAID_UP = monthPayments({ charged: 2400, received: 2400, total: 0 });
const READY_UNITS = unitsSummary({
  readiness: [
    { method: 'idealFraction', ready: true },
    { method: 'equalShare', ready: true },
    { method: 'area', ready: true },
  ],
});

type Answer<T> = T | 'fail' | 'pending';

interface Scenario {
  month?: Answer<MonthEntries>;
  payments?: Answer<MonthPayments>;
  units?: Answer<UnitsSummary>;
  residents?: Answer<number>;
  instructions?: Answer<PaymentInstructions>;
}

interface Harness {
  fixture: ComponentFixture<DashboardPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

function reply(http: HttpTestingController, url: string, body: unknown): void {
  if (body === 'pending') {
    return;
  }
  const request = http.expectOne((r) => r.url === url);
  if (body === 'fail') {
    request.flush(problem(500, 'erro'), { status: 500, statusText: 'Erro' });
  } else {
    request.flush(body);
  }
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    await flushPromises();
    TestBed.tick();
    fixture.detectChanges();
    await fixture.whenStable();
  }
}

async function setup(
  scenario: Scenario = {},
  user: CurrentUser = REGISTERED_MANAGER,
): Promise<Harness> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      { provide: NOW, useValue: () => new Date('2026-11-16T12:00:00Z') },
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  await signInAs(TestBed.inject(SessionStore), http, user);
  const fixture = TestBed.createComponent(DashboardPage);
  fixture.detectChanges();
  if (user.role === 'Manager') {
    const month = scenario.month ?? OPEN_MONTH;
    reply(http, '/api/finance/months/2026-11', month);
    reply(http, '/api/units/summary', scenario.units ?? READY_UNITS);
    reply(
      http,
      '/api/residents',
      scenario.residents === undefined
        ? residentPage([resident()], 37)
        : scenario.residents === 'fail' || scenario.residents === 'pending'
          ? scenario.residents
          : residentPage([resident()], scenario.residents),
    );
    reply(
      http,
      '/api/finance/payment-instructions',
      scenario.instructions ?? paymentInstructions(),
    );
    if (typeof month === 'object' && month.closing !== null) {
      await flushPromises();
      reply(http, '/api/finance/months/2026-11/payments', scenario.payments ?? PAID_UP);
    }
  }
  await settle(fixture);
  return { fixture, http, html: fixture.nativeElement };
}

function byId(html: ParentNode, id: string): HTMLElement | null {
  return html.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function textOf(element: Element | null): string {
  return (element?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

function hrefOf(element: Element | null): string | null {
  return element?.getAttribute('href') ?? null;
}

describe('DashboardPage', () => {
  afterEach(() => TestBed.inject(HttpTestingController).match(() => true));

  it('mostra o título só para leitores de tela e os quatro cartões do síndico', async () => {
    const { html } = await setup();

    const title = html.querySelector('h1');
    expect(title?.textContent?.trim()).toBe('Painel');
    expect(title?.classList).toContain('sr-only');
    expect(html.textContent).not.toContain('Gestão condominial');
    for (const card of ['card-month', 'card-payments', 'card-condominium', 'card-pendings']) {
      expect(byId(html, card)).not.toBeNull();
    }
  });

  it('desenha um esqueleto em cada cartão enquanto espera, sem texto de carregando solto', async () => {
    const { html } = await setup({
      month: 'pending',
      units: 'pending',
      residents: 'pending',
      instructions: 'pending',
    });

    expect(html.querySelectorAll('[data-testid="skeleton"]')).toHaveLength(4);
    expect(html.querySelectorAll('[role="status"]').length).toBeGreaterThanOrEqual(4);
  });

  describe('cartão do mês', () => {
    it('mostra o mês aberto, com o nome do mês e o total das despesas', async () => {
      const { html } = await setup({ month: OPEN_MONTH });
      const card = byId(html, 'card-month')!;

      expect(textOf(card)).toContain('Novembro de 2026');
      expect(textOf(byId(card, 'month-state'))).toBe('Aberto');
      expect(textOf(byId(card, 'month-total'))).toBe(money(4200));
      expect(hrefOf(byId(card, 'month-link'))).toBe('/financeiro/lancamentos');
    });

    it('mostra o mês fechado com o vencimento das cobranças', async () => {
      const { html } = await setup({ month: CLOSED_MONTH });
      const card = byId(html, 'card-month')!;

      expect(textOf(byId(card, 'month-state'))).toBe('Fechado');
      expect(textOf(byId(card, 'month-due'))).toBe('10/12/2026');
    });

    it('diz que o mês ainda não foi aberto', async () => {
      const { html } = await setup({ month: monthEntries() });

      expect(textOf(byId(html, 'month-state'))).toBe('Ainda não aberto');
      expect(byId(html, 'month-total')).toBeNull();
    });

    it('avisa de quanto lançamento está sem valor e de despesa vencida', async () => {
      const month = monthEntries({
        entries: [entry()],
        total: 4200,
        needsAmountCount: 2,
        overdueCount: 1,
      });
      const { html } = await setup({ month });

      expect(textOf(byId(html, 'month-needs-amount'))).toBe('2 aguardando valor');
      expect(textOf(byId(html, 'month-overdue'))).toBe('1 vencida');
    });

    it('falha sozinho, com a nova tentativa, sem derrubar os outros cartões', async () => {
      const { fixture, http, html } = await setup({ month: 'fail' });
      const card = byId(html, 'card-month')!;

      expect(textOf(card)).toContain('Não foi possível carregar o mês');
      expect(byId(html, 'card-condominium')!.textContent).toContain('40');

      byId(card, 'retry')!.click();
      await settle(fixture);
      http.expectOne((r) => r.url === '/api/finance/months/2026-11').flush(OPEN_MONTH);
      await settle(fixture);

      expect(textOf(byId(html, 'month-state'))).toBe('Aberto');
    });
  });

  describe('cartão de recebimentos', () => {
    it('mostra o que foi recebido do cobrado, com a barra e as cobranças em atraso', async () => {
      const payments = monthPayments({ charged: 2400, received: 1300, total: 3 });
      const { html } = await setup({ month: CLOSED_MONTH, payments });
      const card = byId(html, 'card-payments')!;

      expect(textOf(byId(card, 'received'))).toBe(money(1300));
      expect(textOf(byId(card, 'charged'))).toBe(money(2400));
      expect(textOf(byId(card, 'received-percent'))).toBe('54% recebido');
      const bar = card.querySelector('[role="progressbar"]')!;
      expect(bar.getAttribute('aria-valuenow')).toBe('54');
      expect(bar.getAttribute('aria-valuemin')).toBe('0');
      expect(bar.getAttribute('aria-valuemax')).toBe('100');
      expect(textOf(byId(card, 'charges-overdue'))).toBe('3 cobranças em atraso');
      expect(hrefOf(byId(card, 'payments-link'))).toBe('/financeiro/pagamentos');
    });

    it('diz "nenhuma cobrança em atraso" quando está tudo em dia', async () => {
      const { html } = await setup({ month: CLOSED_MONTH, payments: PAID_UP });

      expect(textOf(byId(html, 'charges-overdue'))).toBe('Nenhuma cobrança em atraso');
      expect(textOf(byId(html, 'received-percent'))).toBe('100% recebido');
    });

    it('com 1 cobrança em atraso usa o singular', async () => {
      const payments = monthPayments({ charged: 2400, received: 1200, total: 1 });
      const { html } = await setup({ month: CLOSED_MONTH, payments });

      expect(textOf(byId(html, 'charges-overdue'))).toBe('1 cobrança em atraso');
    });

    it('explica que as cobranças só existem depois de fechar o mês', async () => {
      const { html } = await setup({ month: OPEN_MONTH });
      const card = byId(html, 'card-payments')!;

      expect(textOf(card)).toContain('As cobranças aparecem depois que o mês for fechado');
      expect(card.querySelector('[role="progressbar"]')).toBeNull();
    });

    it('falha sozinho quando só as cobranças não carregam', async () => {
      const { html } = await setup({ month: CLOSED_MONTH, payments: 'fail' });

      expect(textOf(byId(html, 'card-payments'))).toContain(
        'Não foi possível carregar os recebimentos',
      );
      expect(textOf(byId(html, 'month-state'))).toBe('Fechado');
    });
  });

  describe('cartão do condomínio', () => {
    it('mostra quantas unidades ativas e quantos moradores atuais há, com os links', async () => {
      const { html } = await setup({ units: unitsSummary({ active: 40 }), residents: 37 });
      const card = byId(html, 'card-condominium')!;

      expect(textOf(byId(card, 'stat-units'))).toContain('40');
      expect(textOf(byId(card, 'stat-units'))).toContain('Unidades ativas');
      expect(hrefOf(byId(card, 'stat-units'))).toBe('/unidades');
      expect(textOf(byId(card, 'stat-residents'))).toContain('37');
      expect(textOf(byId(card, 'stat-residents'))).toContain('Moradores atuais');
      expect(hrefOf(byId(card, 'stat-residents'))).toBe('/moradores');
    });

    it('falha sozinho, com a nova tentativa', async () => {
      const { fixture, http, html } = await setup({ units: 'fail' });
      const card = byId(html, 'card-condominium')!;

      expect(textOf(card)).toContain('Não foi possível carregar os dados do condomínio');

      byId(card, 'retry')!.click();
      await settle(fixture);
      http.expectOne((r) => r.url === '/api/units/summary').flush(unitsSummary({ active: 12 }));
      http.expectOne((r) => r.url === '/api/residents').flush(residentPage([resident()], 5));
      await settle(fixture);

      expect(textOf(byId(html, 'stat-units'))).toContain('12');
    });
  });

  describe('cartão de pendências', () => {
    it('lista cada pendência com ícone, texto e o link que resolve', async () => {
      const { html } = await setup({
        month: monthEntries({ entries: [entry()], total: 4200, needsAmountCount: 2 }),
        units: READY_UNITS,
        instructions: { pix: null, account: null, updatedAt: null },
      });

      const items = [...html.querySelectorAll<HTMLElement>('[data-testid="pending"]')];
      expect(
        items.map((item) => textOf(item.querySelector('[data-testid="pending-text"]'))),
      ).toEqual([
        'Os dados de pagamento ainda não foram informados',
        '2 lançamentos aguardam o valor',
      ]);
      expect(items[0].querySelector('mat-icon')).not.toBeNull();
      expect(hrefOf(items[0].querySelector('a'))).toBe('/financeiro/dados-de-pagamento');
      expect(textOf(items[0].querySelector('a'))).toBe('Informar dados');
      expect(byId(html, 'all-clear')).toBeNull();
    });

    it('diz "tudo em dia" quando não há pendência', async () => {
      const { html } = await setup({ month: CLOSED_MONTH, units: READY_UNITS });

      expect(byId(html, 'all-clear')).not.toBeNull();
      expect(textOf(byId(html, 'all-clear'))).toContain('Tudo em dia');
      expect(html.querySelectorAll('[data-testid="pending"]')).toHaveLength(0);
    });

    it('não diz "tudo em dia" quando uma leitura falhou: avisa que está incompleto e deixa tentar de novo', async () => {
      const { fixture, http, html } = await setup({
        month: CLOSED_MONTH,
        units: READY_UNITS,
        instructions: 'fail',
      });

      expect(byId(html, 'all-clear')).toBeNull();
      const incomplete = byId(html, 'pendings-incomplete')!;
      expect(textOf(incomplete)).toContain('Não foi possível verificar tudo');

      byId(incomplete, 'retry')!.click();
      await settle(fixture);
      http
        .expectOne((r) => r.url === '/api/finance/payment-instructions')
        .flush(paymentInstructions());
      await settle(fixture);

      expect(byId(html, 'pendings-incomplete')).toBeNull();
      expect(byId(html, 'all-clear')).not.toBeNull();
    });
  });

  it('para quem não é síndico mostra só as boas-vindas, sem chamar a API do condomínio', async () => {
    const { html } = await setup({}, RESIDENT_USER);

    expect(byId(html, 'welcome')).not.toBeNull();
    expect(byId(html, 'card-month')).toBeNull();
    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Painel');
  });

  it('para o morador, leva à sua fatura e deixa os avisos para quando existirem', async () => {
    const { html } = await setup({}, RESIDENT_USER);

    const link = byId(html, 'my-invoice-link');
    expect(link?.getAttribute('href')).toBe('/minha-fatura');
    expect(link?.textContent?.replace(/\s+/g, ' ').trim()).toContain('Ver minha fatura');
    expect(byId(html, 'welcome')?.textContent).not.toContain('suas faturas');
  });

  it('para o síndico, não mostra o atalho da fatura do morador', async () => {
    const { html } = await setup({});

    expect(byId(html, 'my-invoice-link')).toBeNull();
  });
});
