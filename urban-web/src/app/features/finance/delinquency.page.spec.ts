import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { SessionStore } from '../../core/auth/session.store';
import { Delinquency } from '../../core/finance/delinquency.models';
import { formatMoney } from '../../core/finance/money';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, problem, signInAs } from '../../testing/auth.fixtures';
import { debtor, delinquency, overdueCharge } from '../../testing/delinquency.fixtures';
import { DelinquencyPage } from './delinquency.page';

/** formatMoney usa espaço sem quebra entre "R$" e o valor; o texto lido da tela é normalizado para espaço comum. */
function money(amount: number): string {
  return formatMoney(amount).replace(/\s/g, ' ');
}

const LIST = '/api/finance/delinquency';

const SEPTEMBER = overdueCharge({
  chargeId: 'c1',
  month: '2026-09',
  dueDate: '2026-10-10',
  amount: 800,
  paid: 0,
  balance: 800,
  daysOverdue: 36,
});
const OCTOBER = overdueCharge({
  chargeId: 'c2',
  month: '2026-10',
  dueDate: '2026-11-10',
  amount: 800,
  paid: 300,
  balance: 500,
  daysOverdue: 5,
});
const DEBTOR_101 = debtor({
  unitId: 'u1',
  block: 'A',
  number: '101',
  charges: [SEPTEMBER, OCTOBER],
});
const DEBTOR_102 = debtor({
  unitId: 'u2',
  block: 'A',
  number: '102',
  charges: [
    overdueCharge({ chargeId: 'c3', month: '2026-10', amount: 800, balance: 800, paid: 0 }),
  ],
});
const DEBTOR_201 = debtor({
  unitId: 'u3',
  block: null,
  number: '201',
  charges: [
    overdueCharge({ chargeId: 'c4', month: '2026-10', amount: 400, balance: 400, paid: 0 }),
  ],
});

const DATA = delinquency({ items: [DEBTOR_101, DEBTOR_102, DEBTOR_201] });

interface Harness {
  fixture: ComponentFixture<DelinquencyPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

async function setup(data: Delinquency | 'pending' = DATA): Promise<Harness> {
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  await signInAs(TestBed.inject(SessionStore), http, REGISTERED_MANAGER);
  const fixture = TestBed.createComponent(DelinquencyPage);
  fixture.detectChanges();
  if (data !== 'pending') {
    listRequest(http).flush(data);
    await settle(fixture);
  }
  return { fixture, http, html: fixture.nativeElement };
}

function listRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((request) => request.url === LIST);
}

function resendRequest(http: HttpTestingController, chargeId: string): TestRequest {
  return http.expectOne(`/api/finance/charges/${chargeId}/invoice-deliveries`);
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

function debtors(html: HTMLElement): HTMLElement[] {
  return [...html.querySelectorAll<HTMLElement>('[data-testid="debtor"]')];
}

function chargeRows(scope: HTMLElement): HTMLElement[] {
  return [...scope.querySelectorAll<HTMLElement>('[data-testid="charge-row"]')];
}

function resendButtons(html: HTMLElement): HTMLButtonElement[] {
  return [...html.querySelectorAll<HTMLButtonElement>('[data-testid="resend-invoice"]')];
}

function click(element: HTMLElement | null): void {
  element!.click();
}

describe('DelinquencyPage', () => {
  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  describe('carregamento', () => {
    it('mostra que está carregando e lê a primeira página', async () => {
      const { html, http } = await setup('pending');

      expect(testId(html, 'loading')).not.toBeNull();
      const request = listRequest(http);
      expect(request.request.params.get('page')).toBe('1');
      expect(request.request.params.get('pageSize')).toBe('50');
    });

    it('mostra o erro e tenta de novo', async () => {
      const { fixture, html, http } = await setup('pending');
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await settle(fixture);

      expect(testId(html, 'load-error')).not.toBeNull();
      expect(debtors(html)).toHaveLength(0);

      click(testId(html, 'retry'));
      listRequest(http).flush(DATA);
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(debtors(html)).toHaveLength(3);
    });
  });

  describe('resumo', () => {
    it('mostra o total em atraso e quantas unidades devem, de todas as páginas', async () => {
      const { html } = await setup(
        delinquency({ items: [DEBTOR_101], totalOverdue: 9876.5, units: 120 }),
      );

      expect(plain(testId(html, 'stat-total')?.textContent)).toContain(money(9876.5));
      expect(plain(testId(html, 'stat-units')?.textContent)).toContain('120');
    });

    it('diz a que dia o cálculo se refere', async () => {
      const { html } = await setup();

      expect(plain(testId(html, 'as-of')?.textContent)).toBe('Situação em 15/11/2026');
    });

    it('leva aos pagamentos, onde se registra o que foi pago', async () => {
      const { html } = await setup();

      expect(testId(html, 'payments-link')?.getAttribute('href')).toBe('/financeiro/pagamentos');
    });
  });

  describe('unidades devedoras', () => {
    it('mostra uma por unidade, na ordem da API', async () => {
      const { html } = await setup();

      expect(debtors(html).map((item) => plain(testId(item, 'unit')?.textContent))).toEqual([
        'Bloco A · 101',
        'Bloco A · 102',
        '201',
      ]);
    });

    it('mostra quanto cada unidade deve e há quanto tempo', async () => {
      const { html } = await setup();
      const [first, second] = debtors(html);

      expect(plain(testId(first, 'debtor-balance')?.textContent)).toBe(money(1300));
      expect(plain(testId(first, 'debtor-summary')?.textContent)).toBe(
        '2 cobranças em atraso · a mais antiga venceu em 10/10/2026, há 36 dias',
      );
      expect(plain(testId(second, 'debtor-summary')?.textContent)).toBe(
        '1 cobrança em atraso · venceu em 10/11/2026, há 5 dias',
      );
    });

    it('mostra as cobranças em atraso de cada unidade, da mais antiga para a mais nova', async () => {
      const { html } = await setup();
      const rows = chargeRows(debtors(html)[0]);

      expect(rows).toHaveLength(2);
      expect(plain(testId(rows[0], 'month')?.textContent)).toContain('Setembro de 2026');
      expect(plain(testId(rows[0], 'due-date')?.textContent)).toBe('10/10/2026');
      expect(plain(testId(rows[0], 'amount')?.textContent)).toBe(money(800));
      expect(plain(testId(rows[0], 'paid')?.textContent)).toBe(money(0));
      expect(plain(testId(rows[0], 'charge-balance')?.textContent)).toBe(money(800));
      expect(plain(testId(rows[0], 'days')?.textContent)).toBe('36 dias');
      expect(plain(testId(rows[1], 'month')?.textContent)).toContain('Outubro de 2026');
      expect(plain(testId(rows[1], 'paid')?.textContent)).toBe(money(300));
      expect(plain(testId(rows[1], 'charge-balance')?.textContent)).toBe(money(500));
      expect(plain(testId(rows[1], 'days')?.textContent)).toBe('5 dias');
    });

    it('mostra o vermelho de atraso com ícone e texto, nunca só a cor', async () => {
      const { html } = await setup();
      const days = testId(chargeRows(debtors(html)[0])[0], 'days')!;

      expect(days.classList).toContain('text-error');
      expect(days.querySelector('mat-icon')?.getAttribute('data-mat-icon-name')).toBe('error');
    });
  });

  describe('sem inadimplência', () => {
    it('diz que nenhuma unidade está em atraso, sem lista', async () => {
      const { html } = await setup(delinquency({ items: [], totalOverdue: 0, units: 0 }));

      expect(plain(testId(html, 'empty')?.textContent)).toContain('Nenhuma unidade em atraso');
      expect(debtors(html)).toHaveLength(0);
      expect(plain(testId(html, 'stat-total')?.textContent)).toContain(money(0));
      expect(testId(html, 'pagination')).toBeNull();
    });
  });

  describe('paginação', () => {
    it('não aparece quando tudo cabe numa página', async () => {
      const { html } = await setup();

      expect(testId(html, 'pagination')).toBeNull();
    });

    it('pagina pelo total de unidades devedoras', async () => {
      const { fixture, html, http } = await setup(delinquency({ items: [DEBTOR_101], units: 120 }));

      expect(plain(testId(html, 'page-status')?.textContent)).toBe('Página 1 de 3');
      expect((testId(html, 'previous') as HTMLButtonElement).disabled).toBe(true);

      click(testId(html, 'next'));
      const request = listRequest(http);
      expect(request.request.params.get('page')).toBe('2');
      request.flush(delinquency({ items: [DEBTOR_102], units: 120, page: 2 }));
      await settle(fixture);

      expect(plain(testId(html, 'page-status')?.textContent)).toBe('Página 2 de 3');
      expect(plain(testId(debtors(html)[0], 'unit')?.textContent)).toBe('Bloco A · 102');
      expect((testId(html, 'previous') as HTMLButtonElement).disabled).toBe(false);
    });
  });

  describe('reenviar fatura', () => {
    it('tem um botão por cobrança, com o nome da cobrança e da unidade', async () => {
      const { html } = await setup();

      expect(resendButtons(html)).toHaveLength(4);
      expect(plain(resendButtons(html)[0].textContent)).toBe('Reenviar fatura');
      expect(resendButtons(html)[0].getAttribute('aria-label')).toBe(
        'Reenviar fatura de Setembro de 2026 de Bloco A · 101',
      );
      expect(resendButtons(html)[3].getAttribute('aria-label')).toBe(
        'Reenviar fatura de Outubro de 2026 de 201',
      );
    });

    it('põe a fatura na fila e avisa, sem ler a lista de novo', async () => {
      const { fixture, html, http } = await setup();

      click(resendButtons(html)[1]);
      resendRequest(http, 'c2').flush(null, { status: 202, statusText: 'Accepted' });
      await settle(fixture);

      expect(plain(testId(html, 'notice')?.textContent)).toBe(
        'Fatura de Bloco A · 101 (Outubro de 2026) entrou na fila de envio',
      );
      expect(testId(html, 'action-error')).toBeNull();
      expect(debtors(html)).toHaveLength(3);
    });

    it('trava os botões enquanto envia', async () => {
      const { fixture, html, http } = await setup();

      click(resendButtons(html)[0]);
      fixture.detectChanges();

      expect(resendButtons(html).every((button) => button.disabled)).toBe(true);
      expect(resendButtons(html)[0].getAttribute('aria-busy')).toBe('true');
      expect(plain(resendButtons(html)[0].textContent)).toBe('Enviando');

      resendRequest(http, 'c1').flush(null, { status: 202, statusText: 'Accepted' });
      await settle(fixture);

      expect(resendButtons(html).every((button) => !button.disabled)).toBe(true);
      expect(plain(resendButtons(html)[0].textContent)).toBe('Reenviar fatura');
    });

    it('explica a recusa e mantém a lista quando a unidade não tem e-mail', async () => {
      const { fixture, html, http } = await setup();

      click(resendButtons(html)[0]);
      resendRequest(http, 'c1').flush(problem(422, 'finance.invoice_delivery.no_recipient'), {
        status: 422,
        statusText: 'Erro',
      });
      await flushPromises();
      listRequest(http).flush(DATA);
      await settle(fixture);

      expect(plain(testId(html, 'action-error')?.textContent)).toContain(
        'Esta unidade não tem responsável com e-mail cadastrado',
      );
      expect(testId(html, 'notice')).toBeNull();
      expect(debtors(html)).toHaveLength(3);
    });

    it('atualiza a lista quando a cobrança não existe mais', async () => {
      const { fixture, html, http } = await setup();

      click(resendButtons(html)[0]);
      resendRequest(http, 'c1').flush(problem(404, 'finance.charge.not_found'), {
        status: 404,
        statusText: 'Erro',
      });
      await flushPromises();
      listRequest(http).flush(delinquency({ items: [DEBTOR_102, DEBTOR_201] }));
      await settle(fixture);

      expect(plain(testId(html, 'action-error')?.textContent)).toContain(
        'Esta cobrança não existe mais. Atualizamos a lista',
      );
      expect(debtors(html)).toHaveLength(2);
    });

    it('troca o aviso de sucesso pelo erro, e o erro pelo sucesso, de um envio para o outro', async () => {
      const { fixture, html, http } = await setup();
      click(resendButtons(html)[0]);
      resendRequest(http, 'c1').flush(null, { status: 202, statusText: 'Accepted' });
      await settle(fixture);
      expect(testId(html, 'notice')).not.toBeNull();

      click(resendButtons(html)[1]);
      resendRequest(http, 'c2').flush(problem(429, 'rate_limit.exceeded'), {
        status: 429,
        statusText: 'Erro',
      });
      await settle(fixture);

      expect(testId(html, 'notice')).toBeNull();
      expect(testId(html, 'action-error')).not.toBeNull();

      click(resendButtons(html)[1]);
      resendRequest(http, 'c2').flush(null, { status: 202, statusText: 'Accepted' });
      await settle(fixture);

      expect(testId(html, 'action-error')).toBeNull();
      expect(testId(html, 'notice')).not.toBeNull();
    });
  });
});
