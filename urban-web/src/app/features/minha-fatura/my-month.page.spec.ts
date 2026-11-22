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
import { CurrentUser } from '../../core/auth/auth.models';
import { SessionStore } from '../../core/auth/session.store';
import { NOW } from '../../core/finance/clock';
import { formatMoney } from '../../core/finance/money';
import { MyMonth } from '../../core/finance/my-month.models';
import { provideUrbanIcons } from '../../core/theme/icons';
import { FileSaver } from '../../shared/files/file-saver';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, signInAs } from '../../testing/auth.fixtures';
import {
  myCharge,
  myExpenseGroup,
  myExpenses,
  myMonth,
  myUnit,
} from '../../testing/my-month.fixtures';
import { MyMonthPage } from './my-month.page';

/** formatMoney usa espaço sem quebra entre "R$" e o valor; o texto lido da tela é normalizado para espaço comum. */
function money(amount: number): string {
  return formatMoney(amount).replace(/\s/g, ' ');
}

const RESIDENT: CurrentUser = { ...REGISTERED_MANAGER, role: 'Resident' };

const PATH = (month: string): string => `/api/finance/me/months/${month}`;
const INVOICE = (chargeId: string): string => `/api/finance/me/charges/${chargeId}/invoice`;

const ORDINARY = myExpenseGroup({ groupId: 'g1', groupName: 'Despesas ordinárias', total: 1800 });
const WORKS = myExpenseGroup({ groupId: 'g2', groupName: 'Obras', total: 1200 });

const OVERDUE_UNIT = myUnit({
  unitId: 'u1',
  block: null,
  number: '101',
  charge: myCharge({
    chargeId: 'c1',
    dueDate: '2026-11-10',
    amount: 800,
    paid: 300,
    balance: 500,
    status: 'overdue',
    lastReceivedOn: '2026-11-05',
  }),
});

const PAID_UNIT = myUnit({
  unitId: 'u2',
  block: 'A',
  number: '102',
  charge: myCharge({
    chargeId: 'c2',
    dueDate: '2026-11-10',
    amount: 650.5,
    paid: 650.5,
    balance: 0,
    status: 'paid',
    lastReceivedOn: '2026-11-08',
  }),
});

const CLOSED = myMonth({
  units: [OVERDUE_UNIT],
  expenses: myExpenses({ groups: [ORDINARY, WORKS] }),
});

const TWO_UNITS = myMonth({
  units: [OVERDUE_UNIT, PAID_UNIT],
  expenses: myExpenses({ groups: [ORDINARY, WORKS] }),
});

const OPEN = myMonth({ closed: false, units: [myUnit({ charge: null })], expenses: null });

const DEPENDENT = myMonth({ access: 'dependent', units: [], expenses: null });
const NONE = myMonth({ access: 'none', units: [], expenses: null });

interface Harness {
  fixture: ComponentFixture<MyMonthPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

async function setup(
  data: MyMonth | 'pending' = CLOSED,
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
  await signInAs(TestBed.inject(SessionStore), http, RESIDENT);
  if (query !== '') {
    await TestBed.inject(Router).navigateByUrl(`/${query}`);
  }
  const fixture = TestBed.createComponent(MyMonthPage);
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

function cards(html: HTMLElement): HTMLElement[] {
  return [...html.querySelectorAll<HTMLElement>('[data-testid="unit-card"]')];
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

describe('MyMonthPage', () => {
  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match((r) => r.url.startsWith('icons/'));
    http.verify();
  });

  describe('carregamento', () => {
    it('tem o título da tela', async () => {
      const { html } = await setup();

      expect(plain(html.querySelector('h1')?.textContent)).toBe('Minha fatura');
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

    it('abre no mês do endereço (?mes=AAAA-MM)', async () => {
      const { html } = await setup(CLOSED, '?mes=2026-09', '2026-09');

      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Setembro de 2026');
    });

    it('volta ao mês de hoje quando o mês do endereço não vale', async () => {
      const { html } = await setup(CLOSED, '?mes=2099-01');

      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Novembro de 2026');
    });

    it('explica a falha e deixa tentar de novo', async () => {
      const { fixture, html, http } = await setup('pending');
      request(http).flush(null, { status: 500, statusText: 'Erro' });
      await settle(fixture);

      expect(plain(testId(html, 'load-error')?.textContent)).toContain(
        'Não foi possível carregar a sua fatura',
      );
      expect(iconOf(testId(html, 'load-error'))).toBe('error');

      click(testId(html, 'retry'));
      request(http).flush(CLOSED);
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(cards(html)).toHaveLength(1);
    });
  });

  describe('fatura da unidade', () => {
    it('mostra um cartão por unidade, com o título da unidade', async () => {
      const { html } = await setup(TWO_UNITS);

      expect(cards(html).map((card) => plain(testId(card, 'unit-heading')?.textContent))).toEqual([
        'Unidade 101',
        'Bloco A · Unidade 102',
      ]);
    });

    it('mostra vencimento, valor, pago e saldo da cobrança', async () => {
      const { html } = await setup();
      const card = cards(html)[0];

      expect(plain(testId(card, 'due-date')?.textContent)).toBe('Vencimento em 10/11/2026');
      expect(plain(testId(card, 'amount')?.textContent)).toBe(money(800));
      expect(plain(testId(card, 'paid')?.textContent)).toBe(money(300));
      expect(plain(testId(card, 'balance')?.textContent)).toBe(money(500));
    });

    it('diz a situação com texto, ícone e cor: atrasada em vermelho', async () => {
      const { html } = await setup();
      const status = testId(cards(html)[0], 'status');

      expect(plain(status?.textContent)).toBe('Atrasada');
      expect(iconOf(status)).toBe('error');
      expect(status?.classList.contains('text-error')).toBe(true);
    });

    it('diz a situação paga em verde, com o ícone de confere', async () => {
      const { html } = await setup(TWO_UNITS);
      const status = testId(cards(html)[1], 'status');

      expect(plain(status?.textContent)).toBe('Paga');
      expect(iconOf(status)).toBe('check_circle');
      expect(status?.classList.contains('text-success')).toBe(true);
    });

    it.each([
      ['open', 'Em aberto', 'schedule', 'text-warning'],
      ['partial', 'Parcial', 'trending_up', 'text-warning'],
    ] as const)(
      'diz a situação %s em laranja, com texto e ícone',
      async (status, label, icon, tone) => {
        const data = myMonth({
          units: [myUnit({ charge: myCharge({ status, paid: 100, balance: 700 }) })],
        });
        const { html } = await setup(data);
        const element = testId(cards(html)[0], 'status');

        expect(plain(element?.textContent)).toBe(label);
        expect(iconOf(element)).toBe(icon);
        expect(element?.classList.contains(tone)).toBe(true);
      },
    );

    it('diz o dia do último pagamento', async () => {
      const { html } = await setup();

      expect(plain(testId(cards(html)[0], 'last-received')?.textContent)).toBe(
        'Último pagamento em 05/11/2026',
      );
    });

    it('não fala de pagamento quando nada foi pago', async () => {
      const data = myMonth({
        units: [myUnit({ charge: myCharge({ paid: 0, balance: 800, lastReceivedOn: null }) })],
      });
      const { html } = await setup(data);

      expect(testId(cards(html)[0], 'last-received')).toBeNull();
    });

    it('diz o dia a que a situação se refere', async () => {
      const { html } = await setup();

      expect(plain(testId(html, 'as-of')?.textContent)).toBe('Situação em 15/11/2026');
    });

    it('unidade fechada sem cobrança no mês diz que não há cobrança, sem botão de fatura', async () => {
      const data = myMonth({
        units: [myUnit({ number: '103', charge: null })],
        expenses: myExpenses(),
      });
      const { html } = await setup(data);
      const card = cards(html)[0];

      expect(plain(testId(card, 'no-charge')?.textContent)).toBe('Sem cobrança neste mês');
      expect(testId(card, 'download-invoice')).toBeNull();
      expect(testId(card, 'status')).toBeNull();
    });
  });

  describe('despesas do mês', () => {
    it('mostra o total e cada grupo com valor e parte do total', async () => {
      const { html } = await setup();

      expect(plain(testId(html, 'expenses-total')?.textContent)).toBe(money(3000));
      expect(groups(html).map((group) => plain(group.textContent))).toEqual([
        `Despesas ordinárias ${money(1800)} 60%`,
        `Obras ${money(1200)} 40%`,
      ]);
    });

    it('não mostra fornecedor nem observação das despesas', async () => {
      const { html } = await setup();

      expect(testId(html, 'card-expenses')?.textContent).not.toMatch(/fornecedor|observa/i);
    });

    it('não mostra o que o prédio recebeu nem a inadimplência', async () => {
      const { html } = await setup();

      expect(html.textContent).not.toMatch(/inadimpl|recebido do prédio|arrecad/i);
    });
  });

  describe('mês ainda aberto', () => {
    it('explica que a fatura aparece depois do fechamento, sem despesas nem cartão da unidade', async () => {
      const { html } = await setup(OPEN);

      expect(plain(testId(html, 'not-closed')?.textContent)).toBe(
        'A fatura de Novembro de 2026 ainda não foi fechada. Ela aparece aqui depois do fechamento pelo síndico',
      );
      expect(cards(html)).toHaveLength(0);
      expect(testId(html, 'card-expenses')).toBeNull();
    });

    it('o aviso e o ícone saem em laranja (a cor de aviso), não no cinza dos avisos neutros', async () => {
      const { html } = await setup(OPEN);

      const notice = testId(html, 'not-closed')!;

      expect(notice.classList.contains('text-warning')).toBe(true);
      expect(notice.classList.contains('text-on-surface-variant')).toBe(false);
      expect(notice.querySelector('mat-icon')!.getAttribute('data-mat-icon-name')).toBe('schedule');
    });
  });

  describe('quem não tem fatura', () => {
    it('dependente: explica que a fatura fica com o responsável pela unidade', async () => {
      const { html } = await setup(DEPENDENT);

      expect(plain(testId(html, 'dependent-notice')?.textContent)).toContain(
        'A fatura fica com o proprietário ou o inquilino da unidade',
      );
      expect(cards(html)).toHaveLength(0);
      expect(testId(html, 'card-expenses')).toBeNull();
    });

    it('sem unidade: pede para falar com o síndico', async () => {
      const { html } = await setup(NONE);

      expect(plain(testId(html, 'no-unit-notice')?.textContent)).toBe(
        'Você ainda não está vinculado a nenhuma unidade. Fale com o síndico',
      );
      expect(cards(html)).toHaveLength(0);
    });
  });

  describe('baixar a fatura', () => {
    let save: jest.SpiedFunction<FileSaver['save']>;

    beforeEach(() => {
      save = jest.spyOn(FileSaver.prototype, 'save').mockImplementation(() => undefined);
    });

    afterEach(() => save.mockRestore());

    it('tem o botão com texto, só nas unidades com cobrança, e o nome da unidade no rótulo', async () => {
      const { html } = await setup(TWO_UNITS);

      const buttons = cards(html).map((card) => testId(card, 'download-invoice')!);
      expect(buttons.map((button) => plain(button.textContent))).toEqual([
        'Baixar fatura (PDF)',
        'Baixar fatura (PDF)',
      ]);
      expect(buttons.map((button) => button.getAttribute('aria-label'))).toEqual([
        'Baixar fatura (PDF) de Unidade 101',
        'Baixar fatura (PDF) de Bloco A · Unidade 102',
      ]);
      expect(iconOf(buttons[0])).toBe('download');
    });

    it('baixa o PDF pela rota do morador, salva com o nome da API e avisa', async () => {
      const { fixture, html, http } = await setup(TWO_UNITS);

      click(testId(cards(html)[1], 'download-invoice'));
      const pdf = new Blob(['%PDF-1.7'], { type: 'application/pdf' });
      http.expectOne(INVOICE('c2')).flush(pdf, {
        headers: { 'Content-Disposition': "attachment; filename*=UTF-8''fatura-2026-11-a-102.pdf" },
      });
      await settle(fixture);

      expect(save).toHaveBeenCalledTimes(1);
      expect(save).toHaveBeenCalledWith(pdf, 'fatura-2026-11-a-102.pdf');
      expect(plain(testId(html, 'notice')?.textContent)).toBe(
        'Fatura de Bloco A · Unidade 102 baixada',
      );
      expect(testId(html, 'invoice-error')).toBeNull();
    });

    it('trava os botões e mostra que está baixando enquanto o PDF não chega', async () => {
      const { fixture, html, http } = await setup(TWO_UNITS);

      click(testId(cards(html)[0], 'download-invoice'));
      await settle(fixture);

      const first = testId(cards(html)[0], 'download-invoice') as HTMLButtonElement;
      expect(iconOf(first)).toBe('progress_activity');
      expect(first.getAttribute('aria-busy')).toBe('true');
      expect(first.disabled).toBe(true);
      expect((testId(cards(html)[1], 'download-invoice') as HTMLButtonElement).disabled).toBe(true);
      http.expectOne(INVOICE('c1')).flush(new Blob(['%PDF-1.7']));
      await settle(fixture);

      expect((testId(cards(html)[1], 'download-invoice') as HTMLButtonElement).disabled).toBe(
        false,
      );
      expect(iconOf(testId(cards(html)[0], 'download-invoice'))).toBe('download');
      expect(testId(cards(html)[0], 'download-invoice')!.getAttribute('aria-busy')).toBe('false');
    });

    it('explica quando a fatura não está disponível, sem salvar nada', async () => {
      const { fixture, html, http } = await setup();

      click(testId(cards(html)[0], 'download-invoice'));
      http.expectOne(INVOICE('c1')).flush(
        new Blob([JSON.stringify({ status: 404, code: 'finance.charge.not_found' })], {
          type: 'application/problem+json',
        }),
        { status: 404, statusText: 'Not Found' },
      );
      await settle(fixture);

      expect(save).not.toHaveBeenCalled();
      expect(plain(testId(html, 'invoice-error')?.textContent)).toContain(
        'Esta fatura não está disponível',
      );
      expect(testId(html, 'notice')).toBeNull();
    });

    it('explica o limite de requisições e a falta de conexão', async () => {
      const { fixture, html, http } = await setup();

      click(testId(cards(html)[0], 'download-invoice'));
      http.expectOne(INVOICE('c1')).flush(new Blob(['x']), {
        status: 429,
        statusText: 'Too Many Requests',
        headers: { 'Retry-After': '20' },
      });
      await settle(fixture);
      expect(plain(testId(html, 'invoice-error')?.textContent)).toContain('Aguarde 20 segundos');

      click(testId(cards(html)[0], 'download-invoice'));
      http.expectOne(INVOICE('c1')).error(new ProgressEvent('error'));
      await settle(fixture);
      expect(plain(testId(html, 'invoice-error')?.textContent)).toContain(
        'Não foi possível conectar',
      );
    });

    it('troca o aviso de erro pelo de sucesso na próxima tentativa', async () => {
      const { fixture, html, http } = await setup();
      click(testId(cards(html)[0], 'download-invoice'));
      http.expectOne(INVOICE('c1')).flush(new Blob(['x']), { status: 500, statusText: 'Erro' });
      await settle(fixture);
      expect(testId(html, 'invoice-error')).not.toBeNull();

      click(testId(cards(html)[0], 'download-invoice'));
      http.expectOne(INVOICE('c1')).flush(new Blob(['%PDF-1.7']));
      await settle(fixture);

      expect(testId(html, 'invoice-error')).toBeNull();
      expect(testId(html, 'notice')).not.toBeNull();
    });

    it('limpa os avisos ao trocar de mês', async () => {
      const { fixture, html, http } = await setup();
      click(testId(cards(html)[0], 'download-invoice'));
      http.expectOne(INVOICE('c1')).flush(new Blob(['x']), { status: 500, statusText: 'Erro' });
      await settle(fixture);

      click(testId(html, 'previous-month'));
      await settle(fixture);
      request(http, '2026-10').flush(myMonth({ month: '2026-10' }));
      await settle(fixture);

      expect(testId(html, 'invoice-error')).toBeNull();
      expect(testId(html, 'notice')).toBeNull();
    });
  });

  describe('navegação entre meses', () => {
    it('vai ao mês anterior e ao próximo, buscando o mês de cada um', async () => {
      const { fixture, html, http } = await setup();

      click(testId(html, 'previous-month'));
      await settle(fixture);
      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Outubro de 2026');
      request(http, '2026-10').flush(myMonth({ month: '2026-10' }));
      await settle(fixture);

      click(testId(html, 'next-month'));
      await settle(fixture);
      request(http, '2026-11').flush(CLOSED);
      await settle(fixture);

      click(testId(html, 'next-month'));
      await settle(fixture);
      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Dezembro de 2026');
      request(http, '2026-12').flush(myMonth({ month: '2026-12', closed: false, expenses: null }));
      await settle(fixture);

      expect(testId(html, 'not-closed')).not.toBeNull();
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
      request(http, '2026-10').flush(myMonth({ month: '2026-10' }));
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Outubro de 2026');
    });
  });
});
