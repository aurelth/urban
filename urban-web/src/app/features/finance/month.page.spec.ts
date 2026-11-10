import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { SessionStore } from '../../core/auth/session.store';
import { NOW } from '../../core/finance/clock';
import { MonthEntries } from '../../core/finance/finance.models';
import { addMonths } from '../../core/finance/month';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, problem, signInAs } from '../../testing/auth.fixtures';
import { removeOverlays, submitDialog, typeIn } from '../../testing/dialog.harness';
import {
  entry,
  chargeLine,
  groupTotal,
  monthClosing,
  monthEntries,
  unitCharge,
} from '../../testing/finance.fixtures';
import { MonthPage } from './month.page';

const ORDINARY = '0199bbbb-0001-7000-8000-000000000001';
const WORKS = '0199bbbb-0001-7000-8000-000000000002';

const OPEN_2026_11 = { method: 'POST', url: '/api/finance/months/2026-11/open' };

const PORTARIA = entry({
  description: 'Portaria',
  amount: 4200,
  version: '21',
  dueDate: '2026-11-10',
  status: 'paid',
});
const ELEVADOR = entry({
  description: 'Elevador',
  amount: 166.66,
  label: '4/6',
  groupId: WORKS,
  dueDate: '2026-11-20',
  status: 'pending',
});
const LUZ = entry({
  description: 'Conta de luz',
  amount: null,
  needsAmount: true,
  suggestedAmount: 812.4,
  version: '22',
  dueDate: '2026-11-10',
  status: 'overdue',
});
const JARDINAGEM = entry({
  description: 'Jardinagem',
  amount: 300,
  version: '23',
  dueDate: '2026-11-25',
  status: 'canceled',
});

const OPENED = monthEntries({
  entries: [PORTARIA, LUZ, ELEVADOR],
  groups: [
    groupTotal({
      groupId: ORDINARY,
      groupName: 'Despesas ordinárias',
      total: 4200,
      needsAmountCount: 1,
    }),
    groupTotal({ groupId: WORKS, groupName: 'Obras', total: 166.66, needsAmountCount: 0 }),
  ],
  total: 4366.66,
  needsAmountCount: 1,
  overdueCount: 1,
});

const FILLED = monthEntries({
  entries: [PORTARIA, ELEVADOR],
  groups: OPENED.groups,
  total: 4366.66,
  needsAmountCount: 0,
  overdueCount: 0,
});

const CLOSED = monthEntries({
  ...FILLED,
  closing: monthClosing({
    dueDate: '2026-12-10',
    total: 4366.66,
    basis: 'idealFraction',
    charges: [
      unitCharge({ unitId: 'u1', block: null, number: '101', weight: 0.6, amount: 2620 }),
      unitCharge({ unitId: 'u2', block: 'A', number: '102', weight: 0.4, amount: 1746.66 }),
    ],
  }),
});

/** Mês fechado com consumo e fundo de reserva: o total das despesas é R$ 1.000,00 e as cobranças somam mais. */
const CLOSED_WITH_CONSUMPTION = monthEntries({
  ...FILLED,
  closing: monthClosing({
    dueDate: '2026-12-10',
    total: 1000,
    basis: 'idealFraction',
    charges: [
      unitCharge({
        unitId: 'u1',
        number: '101',
        weight: 0.6,
        amount: 803.44,
        lines: [
          chargeLine({ kind: 'expenses', label: 'Contratos', amount: 600 }),
          chargeLine({ kind: 'reserveFund', label: 'Fundo de reserva', amount: 90 }),
          chargeLine({
            kind: 'water',
            label: 'Água',
            amount: 113.44,
            quantity: 17.649,
            previousReading: 100,
            currentReading: 117.649,
          }),
        ],
      }),
      unitCharge({
        unitId: 'u2',
        block: 'A',
        number: '102',
        weight: 0.4,
        amount: 507.43,
        lines: [
          chargeLine({ kind: 'expenses', label: 'Contratos', amount: 400 }),
          chargeLine({ kind: 'reserveFund', label: 'Fundo de reserva', amount: 60 }),
          chargeLine({
            kind: 'water',
            label: 'Água',
            amount: 47.43,
            quantity: 5,
            previousReading: 50,
            currentReading: 55,
          }),
        ],
      }),
    ],
  }),
});

interface Harness {
  fixture: ComponentFixture<MonthPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

async function setup(data: MonthEntries | 'pending' = OPENED): Promise<Harness> {
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
  const fixture = TestBed.createComponent(MonthPage);
  fixture.detectChanges();
  if (data !== 'pending') {
    http.expectOne(OPEN_2026_11).flush(data);
    await settle(fixture);
  }
  return { fixture, http, html: fixture.nativeElement };
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    await flushPromises();
    TestBed.tick();
    fixture.detectChanges();
    await fixture.whenStable();
  }
}

function testId(html: HTMLElement, id: string): HTMLElement | null {
  return html.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function plain(value: string | null | undefined): string {
  return (value ?? '').replace(/\s+/g, ' ').replaceAll(' ', ' ').trim();
}

function text(html: HTMLElement, id: string): string {
  return plain(testId(html, id)?.textContent);
}

function button(html: HTMLElement, label: string): HTMLButtonElement | null {
  return html.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`);
}

function pieces(element: Element): string {
  const walker = document.createTreeWalker(element, NodeFilter.SHOW_TEXT);
  const parts: string[] = [];
  for (let node = walker.nextNode(); node !== null; node = walker.nextNode()) {
    parts.push(node.textContent ?? '');
  }
  return plain(
    parts
      .map((part) => part.trim())
      .filter((part) => part !== '')
      .join(' '),
  );
}

function rowsOf(html: HTMLElement): string[] {
  return [...html.querySelectorAll('[data-testid="entry-row"]')].map(pieces);
}

function statusButton(html: HTMLElement, description: string): HTMLButtonElement {
  return html.querySelector<HTMLButtonElement>(
    `button[aria-label^="Situação de ${description}:"]`,
  )!;
}

async function openStatusMenu(
  fixture: ComponentFixture<unknown>,
  html: HTMLElement,
  description: string,
): Promise<void> {
  statusButton(html, description).click();
  await settle(fixture);
}

function menuItems(): { label: string; disabled: boolean }[] {
  return [...document.querySelectorAll<HTMLButtonElement>('[role="menuitem"]')].map((item) => ({
    label: plain(item.textContent),
    disabled: item.disabled || item.getAttribute('aria-disabled') === 'true',
  }));
}

async function changeStatus(
  fixture: ComponentFixture<unknown>,
  html: HTMLElement,
  description: string,
  label: string,
): Promise<void> {
  await openStatusMenu(fixture, html, description);
  const item = [...document.querySelectorAll<HTMLButtonElement>('[role="menuitem"]')].find(
    (candidate) => plain(candidate.textContent) === label,
  );
  item!.click();
  await settle(fixture);
}

describe('MonthPage', () => {
  afterEach(() => {
    removeOverlays();
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  it('mostra que esta carregando enquanto espera a API', async () => {
    const { html, http } = await setup('pending');

    expect(testId(html, 'loading')).not.toBeNull();
    http.expectOne(OPEN_2026_11);
  });

  it('mostra o erro e tenta de novo', async () => {
    const { fixture, html, http } = await setup('pending');
    http.expectOne(OPEN_2026_11).flush(problem(500, 'x'), { status: 500, statusText: 'Erro' });
    await settle(fixture);

    expect(testId(html, 'load-error')).not.toBeNull();

    testId(html, 'retry')!.click();
    http.expectOne(OPEN_2026_11).flush(OPENED);
    await settle(fixture);

    expect(testId(html, 'load-error')).toBeNull();
    expect(testId(html, 'month-title')).not.toBeNull();
  });

  it('abre no mes atual e aponta para as despesas', async () => {
    const { html } = await setup();

    expect(text(html, 'month-title')).toBe('Novembro de 2026');
    expect(testId(html, 'expenses-link')?.getAttribute('href')).toBe('/financeiro/despesas');
  });

  it('aponta para as tarifas de consumo', async () => {
    const { html } = await setup();

    expect(testId(html, 'tariffs-link')?.getAttribute('href')).toBe('/financeiro/tarifas');
  });

  it('aponta para as leituras de consumo do mesmo mes', async () => {
    const { html } = await setup();

    expect(testId(html, 'readings-link')?.getAttribute('href')).toBe(
      '/financeiro/leituras?mes=2026-11',
    );
  });

  it('gera os lancamentos do mes sozinho, sem botoes para abrir o mes', async () => {
    const { html } = await setup();

    expect(testId(html, 'open-month')).toBeNull();
    expect(plain(html.textContent)).not.toContain('Abrir mês');
    expect(plain(html.textContent)).not.toContain('Gerar lançamentos que faltam');
  });

  it('mantem o titulo e o mes na cor do texto e os rotulos do resumo em cinza', async () => {
    const { html } = await setup();

    for (const element of [html.querySelector('h1'), testId(html, 'month-title')]) {
      expect(element?.classList.contains('text-primary')).toBe(false);
    }
    const labels = ['total', 'needs-amount-count', 'overdue-count'].map((id) =>
      testId(html, id)?.querySelector('dt'),
    );
    for (const label of labels) {
      expect(label?.classList.contains('text-on-surface-variant')).toBe(true);
      expect(label?.classList.contains('text-primary')).toBe(false);
    }
    expect(plain(labels[0]?.textContent)).toBe('Total do mês');
  });

  it('mostra o nome do grupo com o mesmo visual do grupo da tela de despesas', async () => {
    const { html } = await setup();

    for (const title of html.querySelectorAll('[data-testid="group-section"] h2')) {
      expect(title.classList.contains('text-lg')).toBe(true);
      expect(title.classList.contains('font-medium')).toBe(true);
      expect(title.classList.contains('text-primary')).toBe(true);
    }
  });

  it('mostra o total, os valores a informar, os vencidos e os grupos com seus lancamentos', async () => {
    const { html } = await setup();

    expect(text(html, 'total')).toContain('R$ 4.366,66');
    expect(text(html, 'needs-amount-count')).toContain('1 lançamento sem valor');
    expect(text(html, 'overdue-count')).toContain('1 lançamento vencido');
    expect(
      [...html.querySelectorAll('[data-testid="group-section"] h2')].map((title) =>
        plain(title.textContent),
      ),
    ).toEqual(['Despesas ordinárias', 'Obras']);
    expect(rowsOf(html)).toEqual([
      'Portaria Vence em 10/11/2026 R$ 4.200,00 Pago',
      'Conta de luz Vence em 10/11/2026 Valor a informar · sugestão R$ 812,40 Vencido',
      'Elevador Parcela 4/6 Vence em 20/11/2026 R$ 166,66 Pendente',
    ]);
  });

  it('diz que nao ha valores a informar nem vencidos quando esta tudo em dia', async () => {
    const { html } = await setup(
      monthEntries({
        entries: [PORTARIA],
        groups: [groupTotal({ groupId: ORDINARY })],
        total: 4200,
      }),
    );

    expect(text(html, 'needs-amount-count')).toContain('Nenhum lançamento sem valor');
    expect(text(html, 'overdue-count')).toContain('Nenhum lançamento vencido');
  });

  it('conta no plural', async () => {
    const { html } = await setup(
      monthEntries({
        entries: [PORTARIA, LUZ],
        groups: [groupTotal({ groupId: ORDINARY })],
        needsAmountCount: 2,
        overdueCount: 3,
      }),
    );

    expect(text(html, 'needs-amount-count')).toContain('2 lançamentos sem valor');
    expect(text(html, 'overdue-count')).toContain('3 lançamentos vencidos');
  });

  it('mostra o cancelado na lista, como cancelado', async () => {
    const { html } = await setup(
      monthEntries({
        entries: [JARDINAGEM],
        groups: [groupTotal({ groupId: ORDINARY })],
        total: 0,
      }),
    );

    expect(rowsOf(html)).toEqual(['Jardinagem Vence em 25/11/2026 R$ 300,00 Cancelado']);
  });

  it('avisa quando o mes nao tem lancamentos', async () => {
    const { html } = await setup(monthEntries());

    expect(testId(html, 'empty')).not.toBeNull();
    expect(text(html, 'empty')).toContain('Este mês não tem lançamentos');
    expect(testId(html, 'open-month')).toBeNull();
  });

  describe('navegacao', () => {
    async function walk(
      harness: Harness,
      label: string,
      from: string,
      delta: number,
      times: number,
    ): Promise<void> {
      let month = from;
      for (let step = 0; step < times; step++) {
        month = addMonths(month, delta);
        button(harness.html, label)!.click();
        harness.http
          .expectOne({ method: 'POST', url: `/api/finance/months/${month}/open` })
          .flush(monthEntries({ month }));
        await settle(harness.fixture);
      }
    }

    it('volta para o mes anterior, gerando os lancamentos dele', async () => {
      const { fixture, html, http } = await setup();

      button(html, 'Mês anterior')!.click();
      http
        .expectOne({ method: 'POST', url: '/api/finance/months/2026-10/open' })
        .flush(monthEntries({ month: '2026-10' }));
      await settle(fixture);

      expect(text(html, 'month-title')).toBe('Outubro de 2026');
    });

    it('avanca para o mes seguinte', async () => {
      const { fixture, html, http } = await setup();

      button(html, 'Próximo mês')!.click();
      http
        .expectOne({ method: 'POST', url: '/api/finance/months/2026-12/open' })
        .flush(monthEntries({ month: '2026-12' }));
      await settle(fixture);

      expect(text(html, 'month-title')).toBe('Dezembro de 2026');
      expect(button(html, 'Próximo mês')!.disabled).toBe(false);
    });

    it('avanca ate 5 anos a frente e nao passa disso', async () => {
      const harness = await setup();

      await walk(harness, 'Próximo mês', '2026-11', 1, 59);
      expect(text(harness.html, 'month-title')).toBe('Outubro de 2031');
      expect(button(harness.html, 'Próximo mês')!.disabled).toBe(false);

      await walk(harness, 'Próximo mês', '2031-10', 1, 1);
      expect(text(harness.html, 'month-title')).toBe('Novembro de 2031');
      expect(button(harness.html, 'Próximo mês')!.disabled).toBe(true);
    });

    it('volta ate 5 anos atras e nao passa disso', async () => {
      const harness = await setup();

      await walk(harness, 'Mês anterior', '2026-11', -1, 59);
      expect(text(harness.html, 'month-title')).toBe('Dezembro de 2021');
      expect(button(harness.html, 'Mês anterior')!.disabled).toBe(false);

      await walk(harness, 'Mês anterior', '2021-12', -1, 1);
      expect(text(harness.html, 'month-title')).toBe('Novembro de 2021');
      expect(button(harness.html, 'Mês anterior')!.disabled).toBe(true);
    });

    it('limpa os avisos ao trocar de mes', async () => {
      const { fixture, html, http } = await setup();
      button(html, 'Corrigir valor de Portaria')!.click();
      await settle(fixture);
      typeIn('urban-entry-amount-dialog', 'amount', '4300');
      submitDialog('urban-entry-amount-dialog');
      await flushPromises();
      http
        .expectOne({ method: 'PUT', url: `/api/finance/entries/${PORTARIA.id}/amount` })
        .flush(PORTARIA);
      await flushPromises();
      http.expectOne({ method: 'GET', url: '/api/finance/months/2026-11' }).flush(OPENED);
      await settle(fixture);
      expect(testId(html, 'notice')).not.toBeNull();

      button(html, 'Mês anterior')!.click();
      http
        .expectOne({ method: 'POST', url: '/api/finance/months/2026-10/open' })
        .flush(monthEntries({ month: '2026-10' }));
      await settle(fixture);

      expect(testId(html, 'notice')).toBeNull();
    });
  });

  describe('situacao do pagamento', () => {
    it('oferece pendente, pago e cancelado, sem repetir a situacao atual', async () => {
      const { fixture, html } = await setup();

      await openStatusMenu(fixture, html, 'Elevador');

      expect(menuItems()).toEqual([
        { label: 'Pendente', disabled: true },
        { label: 'Pago', disabled: false },
        { label: 'Cancelado', disabled: false },
      ]);
    });

    it('pinta o icone e o texto da situacao com a mesma cor', async () => {
      const { html } = await setup();

      const badge = (description: string): HTMLElement =>
        statusButton(html, description).querySelector<HTMLElement>('[data-testid="status-badge"]')!;

      expect(plain(badge('Portaria').textContent)).toBe('Pago');
      expect(badge('Portaria').classList.contains('text-success')).toBe(true);
      expect(plain(badge('Elevador').textContent)).toBe('Pendente');
      expect(badge('Elevador').classList.contains('text-warning')).toBe(true);
      expect(plain(badge('Conta de luz').textContent)).toBe('Vencido');
      expect(badge('Conta de luz').classList.contains('text-error')).toBe(true);
    });

    it('deixa a seta do menu cinza e so a pinta de destaque com o menu aberto', async () => {
      const { html } = await setup();

      const trigger = statusButton(html, 'Elevador');
      const arrow = trigger.querySelector<HTMLElement>('[data-testid="status-arrow"]')!;

      expect(trigger.classList.contains('group')).toBe(true);
      expect(arrow.classList.contains('[--mat-icon-color:var(--mat-sys-on-surface-variant)]')).toBe(
        true,
      );
      expect(
        arrow.classList.contains('group-aria-expanded:[--mat-icon-color:var(--mat-sys-primary)]'),
      ).toBe(true);
    });

    it('pinta cada opcao do menu com a cor da sua situacao', async () => {
      const { fixture, html } = await setup();

      await openStatusMenu(fixture, html, 'Elevador');

      const tones = [...document.querySelectorAll<HTMLElement>('[role="menuitem"]')].map(
        (item) => item.className,
      );
      expect(tones).toHaveLength(3);
      expect(tones[0]).toContain('--mat-menu-item-icon-color:var(--urban-warning)');
      expect(tones[0]).toContain('--mat-menu-item-label-text-color:var(--urban-warning)');
      expect(tones[1]).toContain('--mat-menu-item-icon-color:var(--urban-success)');
      expect(tones[1]).toContain('--mat-menu-item-label-text-color:var(--urban-success)');
      expect(tones[2]).toContain('--mat-menu-item-icon-color:var(--mat-sys-error)');
      expect(tones[2]).toContain('--mat-menu-item-label-text-color:var(--mat-sys-error)');
    });

    it('trata o vencido como pendente: so se pode marcar pago ou cancelado', async () => {
      const { fixture, html } = await setup(
        monthEntries({
          entries: [{ ...LUZ, amount: 800, needsAmount: false }],
          groups: [groupTotal({ groupId: ORDINARY })],
        }),
      );

      await openStatusMenu(fixture, html, 'Conta de luz');

      expect(menuItems()).toEqual([
        { label: 'Pendente', disabled: true },
        { label: 'Pago', disabled: false },
        { label: 'Cancelado', disabled: false },
      ]);
    });

    it('so deixa marcar como pago depois de informar o valor', async () => {
      const { fixture, html } = await setup();

      await openStatusMenu(fixture, html, 'Conta de luz');

      expect(menuItems()).toEqual([
        { label: 'Pendente', disabled: true },
        { label: 'Pago Informe o valor primeiro', disabled: true },
        { label: 'Cancelado', disabled: false },
      ]);
    });

    it('marca como pago, atualiza o mes e nao mostra aviso (a linha ja mostra a situacao)', async () => {
      const { fixture, html, http } = await setup();

      await changeStatus(fixture, html, 'Elevador', 'Pago');
      const put = http.expectOne({
        method: 'PUT',
        url: `/api/finance/entries/${ELEVADOR.id}/status`,
      });
      expect(put.request.body).toEqual({ version: ELEVADOR.version, status: 'paid' });
      put.flush({ ...ELEVADOR, status: 'paid' });
      await flushPromises();
      http.expectOne({ method: 'GET', url: '/api/finance/months/2026-11' }).flush(
        monthEntries({
          ...OPENED,
          entries: [PORTARIA, LUZ, { ...ELEVADOR, status: 'paid' }],
        }),
      );
      await settle(fixture);

      expect(testId(html, 'notice')).toBeNull();
      expect(rowsOf(html)[2]).toBe('Elevador Parcela 4/6 Vence em 20/11/2026 R$ 166,66 Pago');
    });

    it('desfaz o pago voltando para pendente', async () => {
      const { fixture, html, http } = await setup();

      await changeStatus(fixture, html, 'Portaria', 'Pendente');
      const put = http.expectOne({
        method: 'PUT',
        url: `/api/finance/entries/${PORTARIA.id}/status`,
      });
      expect(put.request.body).toEqual({ version: PORTARIA.version, status: 'pending' });
      put.flush({ ...PORTARIA, status: 'pending' });
      await flushPromises();
      http.expectOne({ method: 'GET', url: '/api/finance/months/2026-11' }).flush(OPENED);
      await settle(fixture);

      expect(testId(html, 'notice')).toBeNull();
    });

    it('cancela o lancamento', async () => {
      const { fixture, html, http } = await setup();

      await changeStatus(fixture, html, 'Elevador', 'Cancelado');
      const put = http.expectOne({
        method: 'PUT',
        url: `/api/finance/entries/${ELEVADOR.id}/status`,
      });
      expect(put.request.body).toEqual({ version: ELEVADOR.version, status: 'canceled' });
      put.flush({ ...ELEVADOR, status: 'canceled' });
      await flushPromises();
      http.expectOne({ method: 'GET', url: '/api/finance/months/2026-11' }).flush(OPENED);
      await settle(fixture);

      expect(testId(html, 'notice')).toBeNull();
    });

    it('explica o erro quando a API recusa e nao avisa sucesso', async () => {
      const { fixture, html, http } = await setup();

      await changeStatus(fixture, html, 'Elevador', 'Pago');
      http
        .expectOne({ method: 'PUT', url: `/api/finance/entries/${ELEVADOR.id}/status` })
        .flush(problem(422, 'expense_entry.amount_required'), {
          status: 422,
          statusText: 'Inválido',
        });
      await settle(fixture);

      expect(text(html, 'action-error')).toBe(
        'Informe o valor do lançamento antes de marcá-lo como pago',
      );
      expect(testId(html, 'notice')).toBeNull();
    });

    it('avisa quando o lancamento mudou em outro lugar e mostra a tela atualizada', async () => {
      const { fixture, html, http } = await setup();

      await changeStatus(fixture, html, 'Elevador', 'Pago');
      http
        .expectOne({ method: 'PUT', url: `/api/finance/entries/${ELEVADOR.id}/status` })
        .flush(problem(409, 'concurrency.conflict'), { status: 409, statusText: 'Conflito' });
      await flushPromises();
      http.expectOne({ method: 'GET', url: '/api/finance/months/2026-11' }).flush(OPENED);
      await settle(fixture);

      expect(text(html, 'action-error')).toBe(
        'Este item foi alterado em outro lugar. A tela já foi atualizada',
      );
    });
  });

  describe('valores', () => {
    it('informa o valor de um lancamento sem valor, ja com a sugestao', async () => {
      const { fixture, html, http } = await setup();

      button(html, 'Informar valor de Conta de luz')!.click();
      await settle(fixture);
      const field = document.querySelector<HTMLInputElement>('[formControlName="amount"]')!;
      expect(field.value).toBe('812,40');
      typeIn('urban-entry-amount-dialog', 'amount', '830,10');
      submitDialog('urban-entry-amount-dialog');
      await flushPromises();
      const save = http.expectOne({ method: 'PUT', url: `/api/finance/entries/${LUZ.id}/amount` });
      expect(save.request.body).toEqual({ version: '22', amount: 830.1 });
      save.flush(LUZ);
      await flushPromises();
      http.expectOne({ method: 'GET', url: '/api/finance/months/2026-11' }).flush(OPENED);
      await settle(fixture);

      expect(document.querySelector('urban-entry-amount-dialog')).toBeNull();
      expect(text(html, 'notice')).toBe('Valor informado');
    });

    it('corrige o valor de um lancamento que ja tinha valor', async () => {
      const { fixture, html, http } = await setup();

      button(html, 'Corrigir valor de Portaria')!.click();
      await settle(fixture);
      typeIn('urban-entry-amount-dialog', 'amount', '4300');
      submitDialog('urban-entry-amount-dialog');
      await flushPromises();
      const save = http.expectOne({
        method: 'PUT',
        url: `/api/finance/entries/${PORTARIA.id}/amount`,
      });
      expect(save.request.body).toEqual({ version: '21', amount: 4300 });
      save.flush(PORTARIA);
      await flushPromises();
      http.expectOne({ method: 'GET', url: '/api/finance/months/2026-11' }).flush(OPENED);
      await settle(fixture);

      expect(text(html, 'notice')).toBe('Valor corrigido');
    });

    it('mantem o dialogo aberto com o erro quando a API recusa', async () => {
      const { fixture, http, html } = await setup();

      button(html, 'Informar valor de Conta de luz')!.click();
      await settle(fixture);
      submitDialog('urban-entry-amount-dialog');
      await flushPromises();
      http
        .expectOne({ method: 'PUT', url: `/api/finance/entries/${LUZ.id}/amount` })
        .flush(problem(422, 'expense.amount.invalid'), { status: 422, statusText: 'Inválido' });
      await settle(fixture);

      expect(
        document.querySelector('urban-entry-amount-dialog [role="alert"]')?.textContent,
      ).toContain('Informe um valor maior que zero');
      expect(testId(html, 'notice')).toBeNull();
    });
  });

  describe('fechamento', () => {
    const CLOSE_2026_11 = { method: 'POST', url: '/api/finance/months/2026-11/close' };
    const REOPEN_2026_11 = { method: 'POST', url: '/api/finance/months/2026-11/reopen' };

    function chargeRows(html: HTMLElement): string[] {
      return [...html.querySelectorAll('[data-testid="charge-row"]')].map(pieces);
    }

    it('oferece fechar o mes quando todos os valores estao informados', async () => {
      const { html } = await setup(FILLED);

      const close = testId(html, 'close-month') as HTMLButtonElement;
      expect(close.disabled).toBe(false);
      expect(plain(close.textContent)).toBe('Fechar mês');
      expect(testId(html, 'close-hint')).toBeNull();
      expect(testId(html, 'closing-card')).toBeNull();
    });

    it('nao deixa fechar enquanto faltam valores e diz por que', async () => {
      const { html } = await setup();

      expect((testId(html, 'close-month') as HTMLButtonElement).disabled).toBe(true);
      expect(text(html, 'close-hint')).toBe('Informe os valores pendentes para poder fechar o mês');
    });

    it('nao oferece fechar um mes sem lancamentos', async () => {
      const { html } = await setup(monthEntries());

      expect(testId(html, 'close-month')).toBeNull();
      expect(testId(html, 'closing-card')).toBeNull();
    });

    it('fecha o mes pelo dialogo, ja com o vencimento sugerido, e mostra as cobrancas', async () => {
      const { fixture, html, http } = await setup(FILLED);

      testId(html, 'close-month')!.click();
      await settle(fixture);
      const dialog = document.querySelector<HTMLElement>('urban-close-month-dialog')!;
      expect(plain(dialog.querySelector('h2')?.textContent)).toBe('Fechar Novembro de 2026');
      expect(document.querySelector<HTMLInputElement>('[formControlName="dueDate"]')!.value).toBe(
        '10/12/2026',
      );
      submitDialog('urban-close-month-dialog');
      await flushPromises();
      const close = http.expectOne(CLOSE_2026_11);
      expect(close.request.body).toEqual({ dueDate: '2026-12-10' });
      close.flush(CLOSED);
      await settle(fixture);

      expect(document.querySelector('urban-close-month-dialog')).toBeNull();
      expect(text(html, 'notice')).toBe('Mês fechado');
      expect(testId(html, 'closing-card')).not.toBeNull();
      expect(testId(html, 'close-month')).toBeNull();
    });

    it('explica no dialogo quando falta a leitura de agua ou gas de alguma unidade', async () => {
      const { fixture, html, http } = await setup(FILLED);

      testId(html, 'close-month')!.click();
      await settle(fixture);
      submitDialog('urban-close-month-dialog');
      await flushPromises();
      http.expectOne(CLOSE_2026_11).flush(problem(422, 'closing.readings_pending'), {
        status: 422,
        statusText: 'Inválido',
      });
      await settle(fixture);

      expect(
        document.querySelector('urban-close-month-dialog [role="alert"]')?.textContent,
      ).toContain('Falta a leitura de água ou gás de alguma unidade');
      expect(
        document
          .querySelector('urban-close-month-dialog [data-testid="failure-link"]')
          ?.getAttribute('href'),
      ).toBe('/financeiro/leituras?mes=2026-11');
      expect(testId(html, 'closing-card')).toBeNull();
    });

    it('explica no dialogo quando as unidades ainda nao bastam para o rateio', async () => {
      const { fixture, html, http } = await setup(FILLED);

      testId(html, 'close-month')!.click();
      await settle(fixture);
      submitDialog('urban-close-month-dialog');
      await flushPromises();
      http.expectOne(CLOSE_2026_11).flush(
        problem(422, 'condominium.apportionment_not_ready', 'detalhe da API', {
          reason: 'fractions_total',
          fractionTotal: 0.05,
        }),
        { status: 422, statusText: 'Inválido' },
      );
      await settle(fixture);

      expect(
        document.querySelector('urban-close-month-dialog [role="alert"]')?.textContent,
      ).toContain('A soma das frações ideais das unidades ativas é 5% e precisa chegar a 100%');
      expect(
        document
          .querySelector('urban-close-month-dialog [data-testid="failure-link"]')
          ?.getAttribute('href'),
      ).toBe('/unidades');
      expect(testId(html, 'closing-card')).toBeNull();
    });

    it('mostra o fechamento: vencimento, metodo, total e a cobranca de cada unidade', async () => {
      const { html } = await setup(CLOSED);

      const card = text(html, 'closing-card');
      expect(card).toContain('Mês fechado');
      expect(card).toContain('Vencimento das cobranças: 10/12/2026');
      expect(card).toContain('Rateio por fração ideal');
      expect(chargeRows(html)).toEqual(['101 60% R$ 2.620,00', 'Bloco A · 102 40% R$ 1.746,66']);
      expect(pieces(testId(html, 'charges-total')!)).toBe('Total das cobranças R$ 4.366,66');
    });

    it('nao repete a linha de despesas quando a cobranca nao tem outras linhas', async () => {
      const { html } = await setup(CLOSED);

      expect(html.querySelectorAll('[data-testid="charge-line"]')).toHaveLength(0);
      expect(testId(html, 'expenses-total')).toBeNull();
    });

    it('detalha as linhas do recibo de cada unidade: grupos, fundo de reserva e consumo', async () => {
      const { html } = await setup(CLOSED_WITH_CONSUMPTION);

      const groups = [...html.querySelectorAll('[data-testid="charge-group"]')];
      expect(groups).toHaveLength(2);
      const lines = (group: Element): string[] =>
        [...group.querySelectorAll('[data-testid="charge-line"]')].map(pieces);
      expect(chargeRows(html)).toEqual(['101 60% R$ 803,44', 'Bloco A · 102 40% R$ 507,43']);
      expect(lines(groups[0])).toEqual([
        'Contratos R$ 600,00',
        'Fundo de reserva R$ 90,00',
        'Água 17,649 m³ (leitura de 100 a 117,649) R$ 113,44',
      ]);
      expect(lines(groups[1])).toEqual([
        'Contratos R$ 400,00',
        'Fundo de reserva R$ 60,00',
        'Água 5 m³ (leitura de 50 a 55) R$ 47,43',
      ]);
    });

    it('mostra o total das despesas e o total das cobrancas quando eles diferem', async () => {
      const { html } = await setup(CLOSED_WITH_CONSUMPTION);

      expect(pieces(testId(html, 'expenses-total')!)).toBe('Despesas do mês R$ 1.000,00');
      expect(pieces(testId(html, 'charges-total')!)).toBe('Total das cobranças R$ 1.310,87');
    });

    it('trava a correcao de valores e o cancelamento, mas deixa marcar como pago', async () => {
      const { fixture, html } = await setup(CLOSED);

      expect(button(html, 'Corrigir valor de Portaria')!.disabled).toBe(true);
      expect(button(html, 'Corrigir valor de Elevador')!.disabled).toBe(true);
      await openStatusMenu(fixture, html, 'Elevador');

      expect(menuItems()).toEqual([
        { label: 'Pendente', disabled: true },
        { label: 'Pago', disabled: false },
        { label: 'Cancelado', disabled: true },
      ]);
    });

    it('no mes aberto continua deixando corrigir valores e cancelar', async () => {
      const { fixture, html } = await setup(FILLED);

      expect(button(html, 'Corrigir valor de Portaria')!.disabled).toBe(false);
      await openStatusMenu(fixture, html, 'Elevador');
      expect(menuItems()[2]).toEqual({ label: 'Cancelado', disabled: false });
    });

    it('reabre o mes pelo dialogo e volta a deixar editar e fechar', async () => {
      const { fixture, html, http } = await setup(CLOSED);

      testId(html, 'reopen-month')!.click();
      await settle(fixture);
      const dialog = document.querySelector<HTMLElement>('urban-reopen-month-dialog')!;
      expect(plain(dialog.querySelector('h2')?.textContent)).toBe('Reabrir Novembro de 2026');
      submitDialog('urban-reopen-month-dialog');
      await flushPromises();
      const reopen = http.expectOne(REOPEN_2026_11);
      expect(reopen.request.body).toEqual({});
      reopen.flush(FILLED);
      await settle(fixture);

      expect(document.querySelector('urban-reopen-month-dialog')).toBeNull();
      expect(text(html, 'notice')).toBe('Mês reaberto');
      expect(testId(html, 'closing-card')).toBeNull();
      expect(testId(html, 'close-month')).not.toBeNull();
      expect(button(html, 'Corrigir valor de Portaria')!.disabled).toBe(false);
    });
  });
});
