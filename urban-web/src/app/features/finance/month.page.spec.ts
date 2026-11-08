import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { SessionStore } from '../../core/auth/session.store';
import { NOW } from '../../core/finance/clock';
import { MonthEntries } from '../../core/finance/finance.models';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, problem, signInAs } from '../../testing/auth.fixtures';
import { removeOverlays, submitDialog, typeIn } from '../../testing/dialog.harness';
import { entry, groupTotal, monthEntries } from '../../testing/finance.fixtures';
import { MonthPage } from './month.page';

const ORDINARY = '0199bbbb-0001-7000-8000-000000000001';
const WORKS = '0199bbbb-0001-7000-8000-000000000002';

const PORTARIA = entry({ description: 'Portaria', amount: 4200, version: '21' });
const ELEVADOR = entry({ description: 'Elevador', amount: 166.66, label: '4/6', groupId: WORKS });
const LUZ = entry({
  description: 'Conta de luz',
  amount: null,
  isPending: true,
  suggestedAmount: 812.4,
  version: '22',
});

const OPENED = monthEntries({
  entries: [PORTARIA, LUZ, ELEVADOR],
  groups: [
    groupTotal({
      groupId: ORDINARY,
      groupName: 'Despesas ordinárias',
      total: 4200,
      pendingCount: 1,
    }),
    groupTotal({ groupId: WORKS, groupName: 'Obras', total: 166.66, pendingCount: 0 }),
  ],
  total: 4366.66,
  pendingCount: 1,
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
    http.expectOne({ method: 'GET', url: '/api/finance/months/2026-11' }).flush(data);
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
    http.expectOne('/api/finance/months/2026-11');
  });

  it('mostra o erro e tenta de novo', async () => {
    const { fixture, html, http } = await setup('pending');
    http
      .expectOne('/api/finance/months/2026-11')
      .flush(problem(500, 'x'), { status: 500, statusText: 'Erro' });
    await settle(fixture);

    expect(testId(html, 'load-error')).not.toBeNull();

    testId(html, 'retry')!.click();
    http.expectOne('/api/finance/months/2026-11').flush(OPENED);
    await settle(fixture);

    expect(testId(html, 'load-error')).toBeNull();
    expect(testId(html, 'month-title')).not.toBeNull();
  });

  it('abre no mes atual e aponta para as despesas', async () => {
    const { html } = await setup();

    expect(text(html, 'month-title')).toBe('Novembro de 2026');
    expect(testId(html, 'expenses-link')?.getAttribute('href')).toBe('/financeiro/despesas');
  });

  it('mostra o total, os pendentes e os grupos com seus lancamentos', async () => {
    const { html } = await setup();

    expect(text(html, 'total')).toContain('R$ 4.366,66');
    expect(text(html, 'pending-count')).toContain('1 lançamento pendente');
    expect(
      [...html.querySelectorAll('[data-testid="group-section"] h2')].map((title) =>
        plain(title.textContent),
      ),
    ).toEqual(['Despesas ordinárias', 'Obras']);
    expect(rowsOf(html)).toEqual([
      'Portaria R$ 4.200,00',
      'Conta de luz Pendente · sugestão R$ 812,40',
      'Elevador Parcela 4/6 R$ 166,66',
    ]);
  });

  it('diz que nao ha pendentes quando tudo tem valor', async () => {
    const { html } = await setup(
      monthEntries({
        entries: [PORTARIA],
        groups: [groupTotal({ groupId: ORDINARY })],
        total: 4200,
      }),
    );

    expect(text(html, 'pending-count')).toContain('Nenhum lançamento pendente');
  });

  describe('mes ainda nao aberto', () => {
    it('explica e oferece abrir', async () => {
      const { html } = await setup(monthEntries());

      expect(testId(html, 'empty')).not.toBeNull();
      expect(text(html, 'open-month')).toBe('Abrir mês');
    });

    it('abre o mes e mostra os lancamentos', async () => {
      const { fixture, html, http } = await setup(monthEntries());

      testId(html, 'open-month')!.click();
      http.expectOne({ method: 'POST', url: '/api/finance/months/2026-11/open' }).flush(OPENED);
      await settle(fixture);

      expect(testId(html, 'empty')).toBeNull();
      expect(rowsOf(html)).toHaveLength(3);
      expect(text(html, 'notice')).toBe('Mês aberto');
      expect(text(html, 'open-month')).toBe('Gerar lançamentos que faltam');
    });

    it('explica o erro quando a API recusa', async () => {
      const { fixture, html, http } = await setup(monthEntries());

      testId(html, 'open-month')!.click();
      http
        .expectOne({ method: 'POST', url: '/api/finance/months/2026-11/open' })
        .flush(problem(422, 'month.too_far_ahead'), { status: 422, statusText: 'Inválido' });
      await settle(fixture);

      expect(text(html, 'action-error')).toBe(
        'Só é possível abrir o mês atual, os anteriores e o mês seguinte',
      );
    });
  });

  describe('mes ja aberto', () => {
    it('gera so o que falta e avisa', async () => {
      const { fixture, html, http } = await setup();

      expect(text(html, 'open-month')).toBe('Gerar lançamentos que faltam');
      testId(html, 'open-month')!.click();
      http.expectOne({ method: 'POST', url: '/api/finance/months/2026-11/open' }).flush(OPENED);
      await settle(fixture);

      expect(text(html, 'notice')).toBe('Lançamentos atualizados');
    });
  });

  describe('navegacao', () => {
    it('volta para o mes anterior', async () => {
      const { fixture, html, http } = await setup();

      button(html, 'Mês anterior')!.click();
      http
        .expectOne({ method: 'GET', url: '/api/finance/months/2026-10' })
        .flush(monthEntries({ month: '2026-10' }));
      await settle(fixture);

      expect(text(html, 'month-title')).toBe('Outubro de 2026');
    });

    it('avanca ate o mes seguinte e nao passa dele', async () => {
      const { fixture, html, http } = await setup();

      button(html, 'Próximo mês')!.click();
      http
        .expectOne({ method: 'GET', url: '/api/finance/months/2026-12' })
        .flush(monthEntries({ month: '2026-12' }));
      await settle(fixture);

      expect(text(html, 'month-title')).toBe('Dezembro de 2026');
      expect(button(html, 'Próximo mês')!.disabled).toBe(true);
    });

    it('limpa os avisos ao trocar de mes', async () => {
      const { fixture, html, http } = await setup();
      testId(html, 'open-month')!.click();
      http.expectOne({ method: 'POST', url: '/api/finance/months/2026-11/open' }).flush(OPENED);
      await settle(fixture);
      expect(testId(html, 'notice')).not.toBeNull();

      button(html, 'Mês anterior')!.click();
      http
        .expectOne({ method: 'GET', url: '/api/finance/months/2026-10' })
        .flush(monthEntries({ month: '2026-10' }));
      await settle(fixture);

      expect(testId(html, 'notice')).toBeNull();
    });
  });

  describe('valores', () => {
    it('informa o valor de um lancamento pendente, ja com a sugestao', async () => {
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
});
