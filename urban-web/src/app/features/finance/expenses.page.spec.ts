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
import { NOW } from '../../core/finance/clock';
import { Expense, ExpenseGroup } from '../../core/finance/finance.models';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, problem, signInAs } from '../../testing/auth.fixtures';
import { chooseIn, removeOverlays, submitDialog, typeIn } from '../../testing/dialog.harness';
import { expense, expenseGroup, installmentExpense } from '../../testing/finance.fixtures';
import { ExpensesPage } from './expenses.page';

const ORDINARY = expenseGroup({ id: '0199bbbb-0001-7000-8000-000000000001', version: '5' });
const WORKS = expenseGroup({ id: '0199bbbb-0001-7000-8000-000000000002', name: 'Obras' });
const FIXED = expense({ description: 'Portaria', supplier: 'Segura Ltda', version: '11' });
const VARIABLE = expense({
  description: 'Conta de luz',
  kind: 'variable',
  currentAmount: null,
  amountChanges: [],
  groupId: WORKS.id,
});
const PARCELS = installmentExpense({ version: '31' });
const ENDED = expense({
  description: 'Jardinagem',
  endMonth: '2026-10',
  lastMonth: '2026-10',
  isEnded: true,
});

interface Options {
  expenses?: Expense[];
  groups?: ExpenseGroup[];
}

interface Harness {
  fixture: ComponentFixture<ExpensesPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

async function setup(options: Options | 'pending' = {}): Promise<Harness> {
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
  const fixture = TestBed.createComponent(ExpensesPage);
  fixture.detectChanges();
  if (options !== 'pending') {
    await answerLoad(http, fixture, options);
  }
  return { fixture, http, html: fixture.nativeElement };
}

async function answerLoad(
  http: HttpTestingController,
  fixture: ComponentFixture<unknown>,
  options: Options = {},
): Promise<void> {
  http.expectOne('/api/finance/expense-groups/default').flush(ORDINARY);
  await flushPromises();
  http
    .expectOne({ method: 'GET', url: '/api/finance/expense-groups' })
    .flush(options.groups ?? [ORDINARY, WORKS]);
  listRequest(http).flush(options.expenses ?? [FIXED, VARIABLE, PARCELS]);
  await settle(fixture);
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    await flushPromises();
    TestBed.tick();
    fixture.detectChanges();
    await fixture.whenStable();
  }
}

function listRequest(http: HttpTestingController): TestRequest {
  return http.expectOne(
    (request) => request.url === '/api/finance/expenses' && request.method === 'GET',
  );
}

function answerRefresh(http: HttpTestingController, expenses: Expense[] = [FIXED]): void {
  http.expectOne({ method: 'GET', url: '/api/finance/expense-groups' }).flush([ORDINARY, WORKS]);
  listRequest(http).flush(expenses);
}

function testId(html: HTMLElement, id: string): HTMLElement | null {
  return html.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function text(html: HTMLElement, id: string): string {
  return plain(testId(html, id)?.textContent ?? '');
}

function plain(value: string): string {
  return value.replace(/\s+/g, ' ').replaceAll(' ', ' ').trim();
}

function rows(html: HTMLElement): HTMLElement[] {
  return [...html.querySelectorAll<HTMLElement>('[data-testid="expense-row"]')];
}

function button(html: HTMLElement, label: string): HTMLButtonElement | null {
  return html.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`);
}

function click(element: HTMLElement | null): void {
  element!.click();
}

function dialog(tag: string): HTMLElement | null {
  return document.querySelector<HTMLElement>(tag);
}

describe('ExpensesPage', () => {
  afterEach(() => {
    removeOverlays();
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  it('mostra que esta carregando enquanto espera a API', async () => {
    const { html, http } = await setup('pending');

    expect(testId(html, 'loading')).not.toBeNull();
    expect(testId(html, 'new-expense')).toBeNull();
    http.expectOne('/api/finance/expense-groups/default');
  });

  it('mostra o erro e tenta de novo', async () => {
    const { fixture, html, http } = await setup('pending');
    http
      .expectOne('/api/finance/expense-groups/default')
      .flush(problem(500, 'x'), { status: 500, statusText: 'Erro' });
    await settle(fixture);

    expect(testId(html, 'load-error')).not.toBeNull();

    click(testId(html, 'retry'));
    await flushPromises();
    await answerLoad(http, fixture);

    expect(testId(html, 'load-error')).toBeNull();
    expect(rows(html)).toHaveLength(3);
  });

  it('lista as despesas com tipo, grupo, valor, periodo e vencimento', async () => {
    const { html } = await setup();

    const cells = rows(html).map((row) =>
      [...row.querySelectorAll('td')].map((cell) => {
        const parts = [...cell.querySelectorAll(':scope > span')];
        return plain(
          parts.length > 0
            ? parts.map((part) => part.textContent).join(' ')
            : (cell.textContent ?? ''),
        );
      }),
    );

    expect(cells[0].slice(0, 5)).toEqual([
      'Portaria Segura Ltda',
      'Despesas ordinárias',
      'Fixa mensal',
      'R$ 4.200,00',
      '09/2026 em diante',
    ]);
    expect(cells[1].slice(0, 5)).toEqual([
      'Conta de luz',
      'Obras',
      'Variável mensal',
      'Informado a cada mês',
      '09/2026 em diante',
    ]);
    expect(cells[2].slice(0, 5)).toEqual([
      'Elevador',
      'Despesas ordinárias',
      'Parcelada',
      'R$ 1.000,00 em 6x',
      '11/2026 a 04/2027',
    ]);
    expect(cells.map((row) => row[5])).toEqual(['Todo dia 10', 'Todo dia 10', 'Todo dia 10']);
    expect(cells[0][6]).toContain('Ativa');
  });

  it('mostra o vencimento: o dia de cada mes, ou a data completa na despesa unica', async () => {
    const { html } = await setup({
      expenses: [
        expense({ description: 'Portaria', dueDay: 5 }),
        expense({
          description: 'Troca da lâmpada',
          kind: 'single',
          startMonth: '2026-11',
          lastMonth: '2026-11',
          dueDay: 12,
        }),
        expense({
          description: 'Reparo do portão',
          kind: 'single',
          startMonth: '2026-11',
          lastMonth: '2026-11',
          dueDay: 31,
        }),
        expense({
          description: 'Pintura',
          kind: 'single',
          startMonth: '2028-02',
          lastMonth: '2028-02',
          dueDay: 31,
        }),
      ],
    });

    const due = rows(html).map((row) => plain(row.querySelectorAll('td')[5].textContent));

    expect(due).toEqual(['Todo dia 5', '12/11/2026', '30/11/2026', '29/02/2028']);
    expect(
      [...html.querySelectorAll('thead th')].map((header) => plain(header.textContent)),
    ).toContain('Vencimento');
  });

  it('aponta para os lancamentos do mes', async () => {
    const { html } = await setup();

    expect(testId(html, 'entries-link')?.getAttribute('href')).toBe('/financeiro/lancamentos');
  });

  it('mostra so as acoes que cabem em cada tipo', async () => {
    const { html } = await setup({ expenses: [FIXED, VARIABLE, PARCELS, ENDED] });

    const labels = rows(html).map((row) =>
      [...row.querySelectorAll('button')].map((item) => item.getAttribute('aria-label')),
    );

    expect(labels).toEqual([
      ['Editar Portaria', 'Reajustar valor de Portaria', 'Encerrar Portaria'],
      ['Editar Conta de luz', 'Encerrar Conta de luz'],
      ['Editar Elevador', 'Alterar parcela de Elevador'],
      ['Editar Jardinagem'],
    ]);
    expect(rows(html)[3].textContent).toContain('Encerrada');
  });

  it('mostra o estado vazio quando nao ha despesas', async () => {
    const { html } = await setup({ expenses: [] });

    expect(testId(html, 'empty')).not.toBeNull();
    expect(testId(html, 'expenses-table')).toBeNull();
  });

  it('filtra por situacao pelo menu, com a opcao atual no botao, e avisa quando nada combina', async () => {
    const { fixture, html, http } = await setup();
    const trigger = testId(html, 'filter-status') as HTMLButtonElement;
    expect(trigger.tagName).toBe('BUTTON');
    expect(plain(trigger.textContent ?? '')).toContain('Ativas');

    click(trigger);
    await settle(fixture);
    const items = [...document.querySelectorAll<HTMLElement>('[role="menuitem"]')];
    expect(items.map((item) => plain(item.textContent ?? ''))).toEqual([
      'Ativas',
      'Encerradas',
      'Todas',
    ]);
    items[1].click();
    const request = listRequest(http);
    expect(request.request.params.get('status')).toBe('ended');
    request.flush([]);
    await settle(fixture);

    expect(plain(trigger.textContent ?? '')).toContain('Encerradas');
    expect(testId(html, 'empty')).toBeNull();
    expect(testId(html, 'no-results')).not.toBeNull();
  });

  it('pinta cada opcao do filtro com a cor do seu icone', async () => {
    const { fixture, html } = await setup();

    click(testId(html, 'filter-status'));
    await settle(fixture);

    const tones = [...document.querySelectorAll<HTMLElement>('[role="menuitem"]')].map(
      (item) => item.className,
    );
    expect(tones).toHaveLength(3);
    expect(tones[0]).toContain('--mat-menu-item-label-text-color:var(--urban-success)');
    expect(tones[1]).toContain(
      '--mat-menu-item-label-text-color:var(--mat-sys-on-surface-variant)',
    );
    expect(tones[2]).toContain('--mat-menu-item-label-text-color:var(--mat-sys-primary)');
  });

  it('usa a cor de destaque nos titulos e no rotulo do filtro', async () => {
    const { html } = await setup();

    const highlighted = [
      html.querySelector('h1'),
      html.querySelector('#groups-title'),
      testId(html, 'filter-label'),
    ];

    for (const element of highlighted) {
      expect(element?.classList.contains('text-primary')).toBe(true);
    }
    expect(text(html, 'filter-label')).toBe('Situação');
  });

  it('pinta o cabecalho da tabela com o fundo do botao Nova despesa, com texto legivel', async () => {
    const { html } = await setup();

    const header = html.querySelector('thead tr')!;

    expect(header.classList.contains('bg-accent-fill')).toBe(true);
    expect(header.classList.contains('text-on-accent-fill')).toBe(true);
    for (const cell of header.querySelectorAll('th')) {
      expect(cell.className).not.toMatch(/text-on-surface/);
    }
  });

  it('mostra Ativa em verde, como o icone, e Encerrada em cinza', async () => {
    const { html } = await setup({ expenses: [FIXED, ENDED] });

    const [active, ended] = rows(html).map((row) =>
      row.querySelector<HTMLElement>('[data-testid="expense-situation"]')!,
    );

    expect(plain(active.textContent ?? '')).toBe('Ativa');
    expect(active.classList.contains('text-success')).toBe(true);
    expect(active.querySelector('[class*="text-on-surface"]')).toBeNull();
    expect(plain(ended.textContent ?? '')).toBe('Encerrada');
    expect(ended.classList.contains('text-on-surface-variant')).toBe(true);
  });

  describe('grupos', () => {
    it('lista os grupos', async () => {
      const { html } = await setup();

      const names = [...html.querySelectorAll('[data-testid="group-item"]')].map((item) =>
        plain(item.textContent ?? ''),
      );

      expect(names).toEqual(['Despesas ordinárias', 'Obras']);
    });

    it('mostra o nome do grupo com o mesmo visual do titulo de grupo dos lancamentos, sem pilula', async () => {
      const { html } = await setup();

      const items = [...html.querySelectorAll('[data-testid="group-item"]')];

      expect(items).toHaveLength(2);
      for (const item of items) {
        const name = item.querySelector('span')!;
        expect(name.classList.contains('text-lg')).toBe(true);
        expect(name.classList.contains('font-medium')).toBe(true);
        expect(name.classList.contains('text-primary')).toBe(true);
        expect(item.className).not.toMatch(/rounded|border/);
      }
    });

    it('cria um grupo', async () => {
      const { fixture, html, http } = await setup();

      click(testId(html, 'new-group'));
      await settle(fixture);
      typeIn('urban-group-dialog', 'name', 'Reformas');
      submitDialog('urban-group-dialog');
      await flushPromises();
      const create = http.expectOne({ method: 'POST', url: '/api/finance/expense-groups' });
      expect(create.request.body).toEqual({ name: 'Reformas' });
      create.flush(expenseGroup({ name: 'Reformas' }));
      await flushPromises();
      answerRefresh(http);
      await settle(fixture);

      expect(dialog('urban-group-dialog')).toBeNull();
      expect(text(html, 'notice')).toBe('Grupo criado');
    });

    it('renomeia um grupo com a versao que a tela tem', async () => {
      const { fixture, html, http } = await setup();

      click(button(html, 'Renomear grupo Despesas ordinárias'));
      await settle(fixture);
      typeIn('urban-group-dialog', 'name', 'Ordinárias');
      submitDialog('urban-group-dialog');
      await flushPromises();
      const rename = http.expectOne({
        method: 'PUT',
        url: `/api/finance/expense-groups/${ORDINARY.id}`,
      });
      expect(rename.request.body).toEqual({ version: '5', name: 'Ordinárias' });
      rename.flush(expenseGroup({ name: 'Ordinárias' }));
      await flushPromises();
      answerRefresh(http);
      await settle(fixture);

      expect(text(html, 'notice')).toBe('Grupo renomeado');
    });

    it('mantem o dialogo aberto com o erro quando o nome ja existe', async () => {
      const { fixture, html, http } = await setup();

      click(testId(html, 'new-group'));
      await settle(fixture);
      typeIn('urban-group-dialog', 'name', 'Obras');
      submitDialog('urban-group-dialog');
      await flushPromises();
      http
        .expectOne({ method: 'POST', url: '/api/finance/expense-groups' })
        .flush(problem(409, 'expense_group.name.already_exists'), {
          status: 409,
          statusText: 'Conflito',
        });
      await settle(fixture);

      expect(dialog('urban-group-dialog')?.querySelector('[role="alert"]')?.textContent).toContain(
        'Já existe um grupo com este nome',
      );
      expect(testId(html, 'notice')).toBeNull();
    });
  });

  describe('despesas', () => {
    it('cadastra uma despesa pelo dialogo', async () => {
      const { fixture, html, http } = await setup();

      click(testId(html, 'new-expense'));
      await settle(fixture);
      typeIn('urban-expense-dialog', 'description', 'Seguro predial');
      typeIn('urban-expense-dialog', 'amount', '900');
      typeIn('urban-expense-dialog', 'dueDay', '10');
      submitDialog('urban-expense-dialog');
      await flushPromises();
      const create = http.expectOne({ method: 'POST', url: '/api/finance/expenses' });
      expect(create.request.body).toEqual({
        kind: 'fixed',
        groupId: ORDINARY.id,
        description: 'Seguro predial',
        supplier: null,
        startMonth: '2026-11',
        endMonth: null,
        amount: 900,
        installmentCount: null,
        currentInstallment: null,
        dueDay: 10,
      });
      create.flush(expense());
      await flushPromises();
      answerRefresh(http);
      await settle(fixture);

      expect(dialog('urban-expense-dialog')).toBeNull();
      expect(text(html, 'notice')).toBe('Despesa cadastrada');
    });

    it('explica o erro da API sem fechar o dialogo', async () => {
      const { fixture, html, http } = await setup();

      click(testId(html, 'new-expense'));
      await settle(fixture);
      typeIn('urban-expense-dialog', 'description', 'Seguro predial');
      typeIn('urban-expense-dialog', 'amount', '900');
      typeIn('urban-expense-dialog', 'dueDay', '10');
      submitDialog('urban-expense-dialog');
      await flushPromises();
      http
        .expectOne({ method: 'POST', url: '/api/finance/expenses' })
        .flush(problem(422, 'expense.end_before_start'), {
          status: 422,
          statusText: 'Inválido',
        });
      await settle(fixture);

      expect(
        dialog('urban-expense-dialog')?.querySelector('[role="alert"]')?.textContent,
      ).toContain('O mês final não pode ser anterior ao mês inicial');
    });

    it('edita descricao, fornecedor e grupo com a versao da despesa', async () => {
      const { fixture, html, http } = await setup();

      click(button(html, 'Editar Portaria'));
      await settle(fixture);
      typeIn('urban-expense-dialog', 'description', 'Portaria 24h');
      chooseIn('urban-expense-dialog', 'groupId', WORKS.id);
      submitDialog('urban-expense-dialog');
      await flushPromises();
      const update = http.expectOne({ method: 'PUT', url: `/api/finance/expenses/${FIXED.id}` });
      expect(update.request.body).toEqual({
        version: '11',
        description: 'Portaria 24h',
        supplier: 'Segura Ltda',
        groupId: WORKS.id,
        dueDay: 10,
      });
      update.flush(FIXED);
      await flushPromises();
      answerRefresh(http);
      await settle(fixture);

      expect(text(html, 'notice')).toBe('Despesa atualizada');
    });

    it('muda o dia do vencimento de uma despesa', async () => {
      const { fixture, html, http } = await setup();

      click(button(html, 'Editar Portaria'));
      await settle(fixture);
      typeIn('urban-expense-dialog', 'dueDay', '25');
      submitDialog('urban-expense-dialog');
      await flushPromises();
      const update = http.expectOne({ method: 'PUT', url: `/api/finance/expenses/${FIXED.id}` });
      expect(update.request.body).toEqual({
        version: '11',
        description: 'Portaria',
        supplier: 'Segura Ltda',
        groupId: ORDINARY.id,
        dueDay: 25,
      });
      update.flush(FIXED);
      await flushPromises();
      answerRefresh(http);
      await settle(fixture);

      expect(text(html, 'notice')).toBe('Despesa atualizada');
    });

    it('reajusta o valor de uma despesa fixa', async () => {
      const { fixture, html, http } = await setup();

      click(button(html, 'Reajustar valor de Portaria'));
      await settle(fixture);
      chooseIn('urban-expense-amount-dialog', 'fromMonth', '2027-01');
      typeIn('urban-expense-amount-dialog', 'amount', '4.400,00');
      submitDialog('urban-expense-amount-dialog');
      await flushPromises();
      const change = http.expectOne({
        method: 'POST',
        url: `/api/finance/expenses/${FIXED.id}/amount`,
      });
      expect(change.request.body).toEqual({ version: '11', fromMonth: '2027-01', amount: 4400 });
      change.flush(FIXED);
      await flushPromises();
      answerRefresh(http);
      await settle(fixture);

      expect(text(html, 'notice')).toBe('Valor reajustado');
    });

    it('altera uma parcela', async () => {
      const { fixture, html, http } = await setup();

      click(button(html, 'Alterar parcela de Elevador'));
      await settle(fixture);
      chooseIn('urban-installment-dialog', 'installment', '3');
      typeIn('urban-installment-dialog', 'amount', '120,50');
      submitDialog('urban-installment-dialog');
      await flushPromises();
      const change = http.expectOne({
        method: 'PUT',
        url: `/api/finance/expenses/${PARCELS.id}/installments/3`,
      });
      expect(change.request.body).toEqual({ version: '31', amount: 120.5 });
      change.flush(PARCELS);
      await flushPromises();
      answerRefresh(http);
      await settle(fixture);

      expect(text(html, 'notice')).toBe('Parcela atualizada');
    });

    it('encerra uma despesa no mes escolhido', async () => {
      const { fixture, html, http } = await setup();

      click(button(html, 'Encerrar Portaria'));
      await settle(fixture);
      chooseIn('urban-end-expense-dialog', 'lastMonth', '2027-03');
      submitDialog('urban-end-expense-dialog');
      await flushPromises();
      const end = http.expectOne({ method: 'POST', url: `/api/finance/expenses/${FIXED.id}/end` });
      expect(end.request.body).toEqual({ version: '11', lastMonth: '2027-03' });
      end.flush(FIXED);
      await flushPromises();
      answerRefresh(http, []);
      await settle(fixture);

      expect(text(html, 'notice')).toBe('Despesa encerrada');
    });

    it('apaga o aviso anterior ao abrir outro dialogo', async () => {
      const { fixture, html, http } = await setup();
      click(testId(html, 'new-group'));
      await settle(fixture);
      typeIn('urban-group-dialog', 'name', 'Reformas');
      submitDialog('urban-group-dialog');
      await flushPromises();
      http.expectOne({ method: 'POST', url: '/api/finance/expense-groups' }).flush(WORKS);
      await flushPromises();
      answerRefresh(http);
      await settle(fixture);
      expect(testId(html, 'notice')).not.toBeNull();

      click(testId(html, 'new-expense'));
      await settle(fixture);

      expect(testId(html, 'notice')).toBeNull();
    });
  });
});
