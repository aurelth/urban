import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { Router, provideRouter } from '@angular/router';
import { SessionStore } from '../../core/auth/session.store';
import { NOW } from '../../core/finance/clock';
import { MonthReadings } from '../../core/finance/finance.models';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, problem, signInAs } from '../../testing/auth.fixtures';
import { meterReading, monthReadings, unitReadings } from '../../testing/finance.fixtures';
import { ReadingsPage } from './readings.page';

const NOV = '/api/finance/months/2026-11/readings';
const DEC = '/api/finance/months/2026-12/readings';

const OPEN = monthReadings({
  hasGas: false,
  units: [
    unitReadings({
      unitId: 'u1',
      block: 'A',
      number: '101',
      suggestedWaterPrevious: 100,
      water: null,
    }),
    unitReadings({
      unitId: 'u2',
      block: 'A',
      number: '102',
      water: meterReading({ previous: 50, current: 60 }),
      suggestedWaterPrevious: 50,
    }),
    unitReadings({ unitId: 'u3', block: null, number: '201' }),
  ],
});

interface Harness {
  fixture: ComponentFixture<ReadingsPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

async function setup(
  data: MonthReadings | 'pending' = OPEN,
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
  const fixture = TestBed.createComponent(ReadingsPage);
  fixture.detectChanges();
  if (data !== 'pending') {
    http.expectOne({ method: 'GET', url: `/api/finance/months/${month}/readings` }).flush(data);
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

function rows(html: HTMLElement): HTMLElement[] {
  return [...html.querySelectorAll<HTMLElement>('[data-testid="unit-row"]')];
}

function field(html: HTMLElement, row: number, name: string): HTMLInputElement {
  return rows(html)[row].querySelector<HTMLInputElement>(`[formControlName="${name}"]`)!;
}

async function typeIn(harness: Harness, row: number, name: string, value: string): Promise<void> {
  const input = field(harness.html, row, name);
  input.value = value;
  input.dispatchEvent(new Event('input'));
  await settle(harness.fixture);
}

function click(element: HTMLElement | null): void {
  element!.click();
}

function button(html: HTMLElement, label: string): HTMLButtonElement | null {
  return html.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`);
}

describe('ReadingsPage', () => {
  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  describe('carregamento', () => {
    it('mostra que esta carregando e ainda nao deixa salvar', async () => {
      const { html, http } = await setup('pending');

      expect(testId(html, 'loading')).not.toBeNull();
      expect(testId(html, 'save-readings')).toBeNull();
      http.expectOne({ method: 'GET', url: NOV });
    });

    it('mostra o erro e tenta de novo', async () => {
      const { fixture, html, http } = await setup('pending');
      http.expectOne({ method: 'GET', url: NOV }).flush(problem(500, 'x'), {
        status: 500,
        statusText: 'Erro',
      });
      await settle(fixture);

      expect(testId(html, 'load-error')).not.toBeNull();

      click(testId(html, 'retry'));
      http.expectOne({ method: 'GET', url: NOV }).flush(OPEN);
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(rows(html)).toHaveLength(3);
    });

    it('abre no mes de hoje, com o titulo do mes', async () => {
      const { html } = await setup();

      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Novembro de 2026');
    });

    it('abre no mes pedido na barra de enderecos, quando cabe na janela de 5 anos', async () => {
      const { html } = await setup(monthReadings({ month: '2026-09' }), '?mes=2026-09', '2026-09');

      expect(plain(testId(html, 'month-title')?.textContent)).toBe('Setembro de 2026');
    });

    it.each(['2026-13', 'abc', '2040-01', '2000-01'])(
      'ignora o mes pedido %s e abre no mes de hoje',
      async (query) => {
        const { html } = await setup(OPEN, `?mes=${query}`);

        expect(plain(testId(html, 'month-title')?.textContent)).toBe('Novembro de 2026');
      },
    );

    it('aponta para as tarifas e para os lancamentos do mes', async () => {
      const { html } = await setup();

      expect(testId(html, 'tariffs-link')?.getAttribute('href')).toBe('/financeiro/tarifas');
      expect(testId(html, 'entries-link')?.getAttribute('href')).toBe('/financeiro/lancamentos');
    });
  });

  describe('unidades', () => {
    it('mostra uma linha por unidade, na ordem da API, com bloco e numero', async () => {
      const { html } = await setup();

      expect(
        rows(html).map((row) =>
          plain(row.querySelector('[data-testid="unit-title"]')?.textContent),
        ),
      ).toEqual(['Bloco A · 101', 'Bloco A · 102', '201']);
    });

    it('sugere a leitura do mes passado como anterior e mostra a leitura ja informada', async () => {
      const { html } = await setup();

      expect(field(html, 0, 'waterPrevious').value).toBe('100');
      expect(field(html, 0, 'waterCurrent').value).toBe('');
      expect(field(html, 1, 'waterPrevious').value).toBe('50');
      expect(field(html, 1, 'waterCurrent').value).toBe('60');
      expect(field(html, 2, 'waterPrevious').value).toBe('0');
    });

    it('mostra o consumo ja lido e o que esta sendo digitado', async () => {
      const harness = await setup();

      expect(
        plain(
          rows(harness.html)[1].querySelector('[data-testid="water-consumption"]')?.textContent,
        ),
      ).toBe('Consumo: 10 m³');
      expect(rows(harness.html)[0].querySelector('[data-testid="water-consumption"]')).toBeNull();

      await typeIn(harness, 0, 'waterCurrent', '112,5');

      expect(
        plain(
          rows(harness.html)[0].querySelector('[data-testid="water-consumption"]')?.textContent,
        ),
      ).toBe('Consumo: 12,5 m³');
    });

    it('conta as unidades com leitura', async () => {
      const { html } = await setup();

      const lines = [...html.querySelectorAll('[data-testid="coverage-line"]')].map((line) =>
        plain(line.textContent),
      );
      expect(lines).toEqual(['Água: 1 de 3 unidades com leitura']);
    });

    it('so mostra o gas quando a tarifa cobra gas', async () => {
      expect(field((await setup()).html, 0, 'gasCurrent')).toBeNull();
      TestBed.resetTestingModule();

      const { html } = await setup(
        monthReadings({ hasGas: true, units: [unitReadings({ suggestedGasPrevious: 7 })] }),
      );

      expect(field(html, 0, 'gasPrevious').value).toBe('7');
      expect(
        [...html.querySelectorAll('[data-testid="coverage-line"]')].map((line) =>
          plain(line.textContent),
        ),
      ).toEqual(['Água: 0 de 1 unidade com leitura', 'Gás: 0 de 1 unidade com leitura']);
    });

    it('avisa quando nao ha tarifa em vigor e quando nao ha unidades', async () => {
      const noTariff = await setup(monthReadings({ hasTariff: false }));
      expect(plain(testId(noTariff.html, 'no-tariff')?.textContent)).toContain(
        'Não há tarifa de consumo em vigor neste mês',
      );
      TestBed.resetTestingModule();

      const empty = await setup(monthReadings({ units: [] }));
      expect(testId(empty.html, 'empty')).not.toBeNull();
      expect(testId(empty.html, 'units-link')?.getAttribute('href')).toBe('/unidades');
      expect(testId(empty.html, 'save-readings')).toBeNull();
    });
  });

  describe('mes fechado', () => {
    it('trava os campos, tira o botao de salvar e explica como corrigir', async () => {
      const { html } = await setup(monthReadings({ isClosed: true, units: OPEN.units }));

      expect(testId(html, 'closed-notice')).not.toBeNull();
      expect(plain(testId(html, 'closed-notice')?.textContent)).toContain('Este mês está fechado');
      expect(field(html, 1, 'waterCurrent').disabled).toBe(true);
      expect(testId(html, 'save-readings')).toBeNull();
    });
  });

  describe('salvar', () => {
    it('so deixa salvar depois de mudar alguma leitura', async () => {
      const harness = await setup();

      expect(testId(harness.html, 'save-readings')?.hasAttribute('disabled')).toBe(true);
      expect(testId(harness.html, 'discard-readings')).toBeNull();

      await typeIn(harness, 0, 'waterCurrent', '110');

      expect(testId(harness.html, 'save-readings')?.hasAttribute('disabled')).toBe(false);
      expect(plain(testId(harness.html, 'dirty-notice')?.textContent)).toBe(
        'Há alterações não salvas',
      );
    });

    it('manda so o que mudou e mostra o aviso quando a API aceita', async () => {
      const harness = await setup();
      await typeIn(harness, 0, 'waterCurrent', '112,5');
      await typeIn(harness, 1, 'waterCurrent', '');

      click(testId(harness.html, 'save-readings'));
      const put = harness.http.expectOne({ method: 'PUT', url: NOV });
      expect(put.request.body).toEqual({
        readings: [
          { unitId: 'u1', service: 'water', previous: 100, current: 112.5 },
          { unitId: 'u2', service: 'water', current: null },
        ],
      });
      put.flush(
        monthReadings({
          units: [
            unitReadings({
              unitId: 'u1',
              water: meterReading({ previous: 100, current: 112.5 }),
            }),
          ],
        }),
      );
      await settle(harness.fixture);

      expect(plain(testId(harness.html, 'notice')?.textContent)).toBe('Leituras salvas');
      expect(rows(harness.html)).toHaveLength(1);
      expect(field(harness.html, 0, 'waterCurrent').value).toBe('112,5');
      expect(testId(harness.html, 'save-readings')?.hasAttribute('disabled')).toBe(true);
      expect(testId(harness.html, 'dirty-notice')).toBeNull();
    });

    it('nao envia nada e leva o foco ao primeiro campo invalido', async () => {
      const harness = await setup();
      await typeIn(harness, 1, 'waterCurrent', 'abc');
      await typeIn(harness, 2, 'waterPrevious', '');

      click(testId(harness.html, 'save-readings'));
      await settle(harness.fixture);

      harness.http.expectNone({ method: 'PUT', url: NOV });
      expect(document.activeElement).toBe(field(harness.html, 1, 'waterCurrent'));
      expect(plain(harness.html.textContent)).toContain(
        'Informe a leitura atual em m³, com até 3 casas decimais',
      );
    });

    it('avisa que falta a leitura atual quando so a anterior foi alterada, sem enviar', async () => {
      const harness = await setup();
      await typeIn(harness, 2, 'waterPrevious', '30');

      click(testId(harness.html, 'save-readings'));
      await settle(harness.fixture);

      harness.http.expectNone({ method: 'PUT', url: NOV });
      expect(plain(testId(harness.html, 'action-error')?.textContent)).toContain(
        'Informe a leitura atual das unidades alteradas',
      );
    });

    it('avisa quando a leitura atual e menor que a anterior, sem enviar', async () => {
      const harness = await setup();
      await typeIn(harness, 0, 'waterCurrent', '99');

      expect(plain(testId(harness.html, 'water-order-error')?.textContent)).toBe(
        'A leitura atual não pode ser menor que a anterior',
      );
      expect(rows(harness.html)[1].querySelector('[data-testid="water-order-error"]')).toBeNull();

      click(testId(harness.html, 'save-readings'));
      await settle(harness.fixture);
      harness.http.expectNone({ method: 'PUT', url: NOV });
    });

    it('explica o que a API recusou e mantem o que foi digitado', async () => {
      const harness = await setup();
      await typeIn(harness, 0, 'waterCurrent', '112,5');

      click(testId(harness.html, 'save-readings'));
      harness.http
        .expectOne({ method: 'PUT', url: NOV })
        .flush(problem(400, 'consumption.reading_invalid'), { status: 400, statusText: 'Erro' });
      await settle(harness.fixture);

      expect(plain(testId(harness.html, 'action-error')?.textContent)).toContain(
        'A leitura atual não pode ser menor que a anterior, e as duas vão de 0 a 9.999.999,999 m³ com até 3 casas',
      );
      expect(field(harness.html, 0, 'waterCurrent').value).toBe('112,5');
      expect(testId(harness.html, 'notice')).toBeNull();
    });

    it('relê o mes e avisa quando ele foi fechado em outro lugar', async () => {
      const harness = await setup();
      await typeIn(harness, 0, 'waterCurrent', '112,5');

      click(testId(harness.html, 'save-readings'));
      harness.http
        .expectOne({ method: 'PUT', url: NOV })
        .flush(problem(409, 'closing.month_is_closed'), { status: 409, statusText: 'Conflito' });
      await flushPromises();
      harness.http
        .expectOne({ method: 'GET', url: NOV })
        .flush(monthReadings({ isClosed: true, units: OPEN.units }));
      await settle(harness.fixture);

      expect(plain(testId(harness.html, 'action-error')?.textContent)).toContain(
        'O mês está fechado',
      );
      expect(testId(harness.html, 'closed-notice')).not.toBeNull();
      expect(testId(harness.html, 'save-readings')).toBeNull();
    });

    it('limpa o aviso de sucesso ao voltar a mexer nas leituras', async () => {
      const harness = await setup();
      await typeIn(harness, 0, 'waterCurrent', '112,5');
      click(testId(harness.html, 'save-readings'));
      harness.http.expectOne({ method: 'PUT', url: NOV }).flush(OPEN);
      await settle(harness.fixture);
      expect(testId(harness.html, 'notice')).not.toBeNull();

      await typeIn(harness, 0, 'waterCurrent', '120');

      expect(testId(harness.html, 'notice')).toBeNull();
    });
  });

  describe('descartar', () => {
    it('volta as leituras ao que esta salvo', async () => {
      const harness = await setup();
      await typeIn(harness, 0, 'waterCurrent', '112,5');
      await typeIn(harness, 1, 'waterPrevious', '40');

      click(testId(harness.html, 'discard-readings'));
      await settle(harness.fixture);

      expect(field(harness.html, 0, 'waterCurrent').value).toBe('');
      expect(field(harness.html, 1, 'waterPrevious').value).toBe('50');
      expect(testId(harness.html, 'dirty-notice')).toBeNull();
      expect(testId(harness.html, 'discard-readings')).toBeNull();
    });
  });

  describe('troca de mes', () => {
    it('vai para o proximo mes e volta', async () => {
      const harness = await setup();

      click(button(harness.html, 'Próximo mês'));
      harness.http
        .expectOne({ method: 'GET', url: DEC })
        .flush(monthReadings({ month: '2026-12' }));
      await settle(harness.fixture);

      expect(plain(testId(harness.html, 'month-title')?.textContent)).toBe('Dezembro de 2026');

      click(button(harness.html, 'Mês anterior'));
      harness.http.expectOne({ method: 'GET', url: NOV }).flush(OPEN);
      await settle(harness.fixture);

      expect(plain(testId(harness.html, 'month-title')?.textContent)).toBe('Novembro de 2026');
    });

    it('nao troca de mes enquanto ha leituras nao salvas', async () => {
      const harness = await setup();
      await typeIn(harness, 0, 'waterCurrent', '112,5');

      expect(button(harness.html, 'Próximo mês')?.disabled).toBe(true);
      expect(button(harness.html, 'Mês anterior')?.disabled).toBe(true);
    });
  });
});
