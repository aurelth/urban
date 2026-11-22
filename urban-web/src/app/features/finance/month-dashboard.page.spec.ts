import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { Router, provideRouter } from '@angular/router';
import { SessionStore } from '../../core/auth/session.store';
import { NOW } from '../../core/finance/clock';
import { MonthDashboard } from '../../core/finance/month-dashboard.models';
import { formatMoney } from '../../core/finance/money';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, signInAs } from '../../testing/auth.fixtures';
import {
  dashboardCharges,
  dashboardExpenses,
  dashboardGroup,
  monthDashboard,
} from '../../testing/month-dashboard.fixtures';
import { MonthDashboardPage } from './month-dashboard.page';

/** formatMoney usa espaço sem quebra entre "R$" e o valor; o texto lido da tela é normalizado para espaço comum. */
function money(amount: number): string {
  return formatMoney(amount).replace(/\s/g, ' ');
}

const PATH = (month: string): string => `/api/finance/months/${month}/dashboard`;

const ORDINARY = dashboardGroup({ groupId: 'g1', groupName: 'Despesas ordinárias', total: 1800 });
const WORKS = dashboardGroup({ groupId: 'g2', groupName: 'Obras', total: 1200 });

const CLOSED = monthDashboard({
  expenses: dashboardExpenses({ groups: [ORDINARY, WORKS] }),
  charges: dashboardCharges({
    charged: 2400,
    received: 1300,
    open: 0,
    partial: 1,
    paid: 1,
    overdue: 1,
    overdueAmount: 800,
  }),
});

const OPEN = monthDashboard({
  closed: false,
  charges: null,
  expenses: dashboardExpenses({ groups: [ORDINARY], needsAmountCount: 2, overdueCount: 1 }),
});

const NOT_OPENED = monthDashboard({
  closed: false,
  charges: null,
  expenses: dashboardExpenses({ groups: [], total: 0 }),
});

interface Harness {
  fixture: ComponentFixture<MonthDashboardPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

async function setup(
  data: MonthDashboard | 'pending' = CLOSED,
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
  const fixture = TestBed.createComponent(MonthDashboardPage);
  fixture.detectChanges();
  if (data !== 'pending') {
    request(http, month).flush(data);
    await settle(fixture);
  }
  return { fixture, http, html: fixture.nativeElement };
}

function request(http: HttpTestingController, month = '2026-11'): TestRequest {
  return http.expectOne((r) => r.url === PATH(month));
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

function groups(html: HTMLElement): HTMLElement[] {
  return [...html.querySelectorAll<HTMLElement>('[data-testid="group"]')];
}

function iconOf(element: HTMLElement | null): string | null | undefined {
  return element?.querySelector('mat-icon')?.getAttribute('data-mat-icon-name');
}

function click(element: HTMLElement | null): void {
  element!.click();
}

describe('MonthDashboardPage', () => {
  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match((r) => r.url.startsWith('icons/'));
    http.verify();
  });

  describe('carregamento', () => {
    it('tem o título da tela', async () => {
      const { html } = await setup();

      expect(plain(html.querySelector('h1')?.textContent)).toBe('Painel financeiro');
    });

    it('mostra que está carregando', async () => {
      const { html, http } = await setup('pending');

      expect(testId(html, 'loading')).not.toBeNull();
      request(http);
    });

    it('abre no mês de hoje, com o título do mês', async () => {
      const { html } = await setup();

      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Novembro de 2026');
    });

    it('abre no mês pedido na barra de endereços, quando cabe na janela de 5 anos', async () => {
      const { html } = await setup(CLOSED, '?mes=2026-09', '2026-09');

      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Setembro de 2026');
    });

    it('volta ao mês de hoje quando o mês pedido não é válido ou está fora da janela', async () => {
      const { html } = await setup(CLOSED, '?mes=2099-01');

      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Novembro de 2026');
    });

    it('mostra o erro e tenta de novo o mesmo mês', async () => {
      const { fixture, html, http } = await setup('pending');
      request(http).flush(null, { status: 500, statusText: 'Erro' });
      await settle(fixture);

      expect(testId(html, 'load-error')).not.toBeNull();
      expect(testId(html, 'card-expenses')).toBeNull();

      click(testId(html, 'retry'));
      request(http).flush(CLOSED);
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(testId(html, 'card-expenses')).not.toBeNull();
    });
  });

  describe('despesas', () => {
    it('mostra o total do mês e um item por grupo, com o valor e a parte do total', async () => {
      const { html } = await setup();

      expect(plain(testId(html, 'expenses-total')?.textContent)).toBe(money(3000));
      expect(groups(html).map((item) => plain(item.textContent))).toEqual([
        `Despesas ordinárias ${money(1800)} 60%`,
        `Obras ${money(1200)} 40%`,
      ]);
    });

    it('não avisa de nada quando tudo está preenchido e em dia', async () => {
      const { html } = await setup();

      expect(testId(html, 'needs-amount')).toBeNull();
      expect(testId(html, 'expenses-overdue')).toBeNull();
    });

    it('avisa dos lançamentos sem valor e das despesas vencidas, em laranja e com ícone', async () => {
      const { html } = await setup(OPEN);

      expect(plain(testId(html, 'needs-amount')?.textContent)).toBe(
        '2 lançamentos aguardam o valor',
      );
      expect(plain(testId(html, 'expenses-overdue')?.textContent)).toBe('1 despesa vencida');
      expect(iconOf(testId(html, 'needs-amount'))).toBe('warning');
      expect(testId(html, 'needs-amount')?.classList.contains('text-warning')).toBe(true);
    });

    it('explica que o mês ainda não foi aberto e leva aos lançamentos do mês', async () => {
      const { html } = await setup(NOT_OPENED);

      expect(plain(testId(html, 'no-expenses')?.textContent)).toContain(
        'Nenhuma despesa neste mês',
      );
      expect(groups(html)).toHaveLength(0);
      expect(testId(html, 'entries-link')?.getAttribute('href')).toBe(
        '/financeiro/lancamentos?mes=2026-11',
      );
    });

    it('leva aos lançamentos do mês que está na tela', async () => {
      const { html } = await setup(CLOSED, '?mes=2026-09', '2026-09');

      expect(testId(html, 'entries-link')?.getAttribute('href')).toBe(
        '/financeiro/lancamentos?mes=2026-09',
      );
    });
  });

  describe('cobranças', () => {
    it('mostra o vencimento, o recebido do cobrado, a parte recebida e o que falta receber', async () => {
      const { html } = await setup();

      expect(plain(testId(html, 'due-date')?.textContent)).toBe('Vencimento em 10/11/2026');
      expect(plain(testId(html, 'received')?.textContent)).toBe(money(1300));
      expect(plain(testId(html, 'received-percent')?.textContent)).toBe('54% recebido');
      expect(plain(testId(html, 'charged')?.textContent)).toBe(money(2400));
      expect(plain(testId(html, 'to-receive')?.textContent)).toBe(`A receber ${money(1100)}`);
      const bar = html.querySelector('[role="progressbar"]');
      expect(bar?.getAttribute('aria-valuenow')).toBe('54');
    });

    it('conta as unidades de cada situação, com ícone e texto', async () => {
      const { html } = await setup();

      expect(plain(testId(html, 'situation-paid')?.textContent)).toBe('Pagas 1');
      expect(plain(testId(html, 'situation-open')?.textContent)).toBe('Em aberto 0');
      expect(plain(testId(html, 'situation-partial')?.textContent)).toBe('Parciais 1');
      expect(plain(testId(html, 'situation-overdue')?.textContent)).toBe('Atrasadas 1');
      expect(iconOf(testId(html, 'situation-paid'))).toBe('check_circle');
      expect(iconOf(testId(html, 'situation-overdue'))).toBe('error');
    });

    it('mostra o saldo atrasado do mês em vermelho e leva à inadimplência', async () => {
      const { html } = await setup();

      expect(plain(testId(html, 'charges-overdue')?.textContent)).toBe('1 cobrança em atraso');
      expect(plain(testId(html, 'overdue-amount')?.textContent)).toBe(
        `${money(800)} em atraso neste mês`,
      );
      expect(testId(html, 'charges-overdue')?.classList.contains('text-error')).toBe(true);
      expect(testId(html, 'delinquency-link')?.getAttribute('href')).toBe(
        '/financeiro/inadimplencia',
      );
    });

    it('sem atraso, diz que está em dia, em verde, e não leva à inadimplência', async () => {
      const data = monthDashboard({
        charges: dashboardCharges({
          charged: 2400,
          received: 2400,
          open: 0,
          partial: 0,
          paid: 3,
          overdue: 0,
          overdueAmount: 0,
        }),
      });
      const { html } = await setup(data);

      expect(plain(testId(html, 'charges-overdue')?.textContent)).toBe(
        'Nenhuma cobrança em atraso',
      );
      expect(testId(html, 'charges-overdue')?.classList.contains('text-success')).toBe(true);
      expect(iconOf(testId(html, 'charges-overdue'))).toBe('check_circle');
      expect(testId(html, 'overdue-amount')).toBeNull();
      expect(testId(html, 'delinquency-link')).toBeNull();
      expect(plain(testId(html, 'received-percent')?.textContent)).toBe('100% recebido');
    });

    it('diz o dia a que a situação se refere', async () => {
      const { html } = await setup();

      expect(plain(testId(html, 'as-of')?.textContent)).toBe('Situação em 15/11/2026');
    });

    it('leva aos pagamentos do mês que está na tela', async () => {
      const { html } = await setup(CLOSED, '?mes=2026-09', '2026-09');

      expect(testId(html, 'payments-link')?.getAttribute('href')).toBe(
        '/financeiro/pagamentos?mes=2026-09',
      );
    });

    it('com o mês aberto, explica que as cobranças aparecem depois do fechamento', async () => {
      const { html } = await setup(OPEN);

      expect(plain(testId(html, 'not-closed')?.textContent)).toContain(
        'As cobranças de Novembro de 2026 aparecem depois que o mês for fechado',
      );
      expect(testId(html, 'close-link')?.getAttribute('href')).toBe(
        '/financeiro/lancamentos?mes=2026-11',
      );
      expect(testId(html, 'received')).toBeNull();
      expect(testId(html, 'payments-link')).toBeNull();
    });
  });

  describe('navegação entre meses', () => {
    it('vai ao mês anterior e ao próximo, buscando o painel de cada um', async () => {
      const { fixture, html, http } = await setup();

      click(testId(html, 'previous-month'));
      await settle(fixture);
      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Outubro de 2026');
      request(http, '2026-10').flush(monthDashboard({ month: '2026-10' }));
      await settle(fixture);

      click(testId(html, 'next-month'));
      await settle(fixture);
      request(http, '2026-11').flush(CLOSED);
      await settle(fixture);

      click(testId(html, 'next-month'));
      await settle(fixture);
      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Dezembro de 2026');
      request(http, '2026-12').flush(NOT_OPENED);
      await settle(fixture);

      expect(testId(html, 'no-expenses')).not.toBeNull();
    });

    it('trava as setas enquanto carrega', async () => {
      const { html, http } = await setup('pending');

      expect((testId(html, 'previous-month') as HTMLButtonElement).disabled).toBe(true);
      expect((testId(html, 'next-month') as HTMLButtonElement).disabled).toBe(true);
      request(http);
    });

    it('não passa de 5 anos à frente nem para trás', async () => {
      const last = await setup(CLOSED, '?mes=2031-11', '2031-11');

      expect((testId(last.html, 'next-month') as HTMLButtonElement).disabled).toBe(true);
      expect((testId(last.html, 'previous-month') as HTMLButtonElement).disabled).toBe(false);
    });

    it('tenta de novo o mês que está na tela depois de um erro', async () => {
      const { fixture, html, http } = await setup();
      click(testId(html, 'previous-month'));
      await settle(fixture);
      request(http, '2026-10').flush(null, { status: 500, statusText: 'Erro' });
      await settle(fixture);

      click(testId(html, 'retry'));
      request(http, '2026-10').flush(monthDashboard({ month: '2026-10' }));
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Outubro de 2026');
    });
  });
});
