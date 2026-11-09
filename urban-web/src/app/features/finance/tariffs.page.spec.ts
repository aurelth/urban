import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { SessionStore } from '../../core/auth/session.store';
import { NOW } from '../../core/finance/clock';
import { ConsumptionTariff } from '../../core/finance/finance.models';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, problem, signInAs } from '../../testing/auth.fixtures';
import { removeOverlays, submitDialog, typeIn } from '../../testing/dialog.harness';
import { consumptionTariff, tariffBand } from '../../testing/finance.fixtures';
import { TariffsPage } from './tariffs.page';

const TARIFFS_URL = '/api/finance/consumption-tariffs';
const SETTINGS_URL = '/api/finance/settings';

const UPCOMING = consumptionTariff({ validFrom: '2027-01' });
const CURRENT = consumptionTariff({
  validFrom: '2026-11',
  sewage: [tariffBand({ upToCubicMeters: 10 }), tariffBand()],
  sewagePercent: 50,
  gas: { kilosPerCubicMeter: 2.4, pricePerKilo: 15.71, pricePerCubicMeter: 37.7 },
});
const OLD = consumptionTariff({ validFrom: '2026-03' });

interface Options {
  tariffs?: ConsumptionTariff[];
  reserveFundPercent?: number;
}

interface Harness {
  fixture: ComponentFixture<TariffsPage>;
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
  const fixture = TestBed.createComponent(TariffsPage);
  fixture.detectChanges();
  if (options !== 'pending') {
    answerFetch(http, options);
    await settle(fixture);
  }
  return { fixture, http, html: fixture.nativeElement };
}

function answerFetch(http: HttpTestingController, options: Options = {}): void {
  http
    .expectOne({ method: 'GET', url: TARIFFS_URL })
    .flush(options.tariffs ?? [UPCOMING, CURRENT, OLD]);
  http
    .expectOne({ method: 'GET', url: SETTINGS_URL })
    .flush({ reserveFundPercent: options.reserveFundPercent ?? 5 });
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
  return (value ?? '').replace(/\s+/g, ' ').replaceAll(' ', ' ').trim();
}

function items(html: HTMLElement): HTMLElement[] {
  return [...html.querySelectorAll<HTMLElement>('[data-testid="tariff-item"]')];
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

function typeInWater(price: string): void {
  const input = document.querySelector<HTMLInputElement>(
    'urban-tariff-dialog [data-testid="water-bands"] [formControlName="price"]',
  )!;
  input.value = price;
  input.dispatchEvent(new Event('input'));
}

describe('TariffsPage', () => {
  afterEach(() => {
    removeOverlays();
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  it('mostra que esta carregando enquanto espera a API', async () => {
    const { html, http } = await setup('pending');

    expect(testId(html, 'loading')).not.toBeNull();
    expect(testId(html, 'new-tariff')).toBeNull();
    http.expectOne({ method: 'GET', url: TARIFFS_URL });
    http.expectOne({ method: 'GET', url: SETTINGS_URL });
  });

  it('mostra o erro e tenta de novo', async () => {
    const { fixture, html, http } = await setup('pending');
    http
      .expectOne({ method: 'GET', url: TARIFFS_URL })
      .flush(problem(500, 'x'), { status: 500, statusText: 'Erro' });
    http.expectOne({ method: 'GET', url: SETTINGS_URL }).flush({ reserveFundPercent: 0 });
    await settle(fixture);

    expect(testId(html, 'load-error')).not.toBeNull();

    click(testId(html, 'retry'));
    answerFetch(http);
    await settle(fixture);

    expect(testId(html, 'load-error')).toBeNull();
    expect(items(html)).toHaveLength(3);
  });

  it('aponta para as despesas', async () => {
    const { html } = await setup();

    expect(testId(html, 'expenses-link')?.getAttribute('href')).toBe('/financeiro/despesas');
  });

  describe('lista', () => {
    it('mostra as tarifas da mais nova para a mais antiga, com o que cada uma cobra', async () => {
      const { html } = await setup();

      const headings = items(html).map((item) =>
        plain(item.querySelector('[data-testid="tariff-title"]')?.textContent),
      );
      expect(headings).toEqual([
        'A partir de janeiro de 2027',
        'A partir de novembro de 2026',
        'A partir de março de 2026',
      ]);
      const lines = [...items(html)[1].querySelectorAll('[data-testid="tariff-line"]')].map(
        (line) => plain(line.textContent),
      );
      expect(lines).toEqual([
        'Água 3 faixas',
        'Esgoto 2 faixas, cobrando 50% do valor',
        'Gás 2,4 kg/m³ × R$ 15,71 por kg = R$ 37,70 por m³',
      ]);
      expect(plain(items(html)[0].textContent)).toContain('Esgoto Não cobrado');
    });

    it('marca com texto e icone so a tarifa em vigor no mes de hoje', async () => {
      const { html } = await setup();

      const badges = html.querySelectorAll('[data-testid="in-force-badge"]');

      expect(badges).toHaveLength(1);
      expect(items(html)[1].contains(badges[0])).toBe(true);
      expect(plain(badges[0].textContent)).toBe('Em vigor');
      expect(badges[0].querySelector('mat-icon')).not.toBeNull();
    });

    it('mostra o estado vazio quando nao ha tarifa e ainda deixa cadastrar', async () => {
      const { html } = await setup({ tariffs: [] });

      expect(testId(html, 'empty')).not.toBeNull();
      expect(items(html)).toHaveLength(0);
      expect(testId(html, 'new-tariff')).not.toBeNull();
    });
  });

  describe('fundo de reserva', () => {
    it('mostra o percentual sobre os grupos da base', async () => {
      const { html } = await setup({ reserveFundPercent: 2.5 });

      expect(plain(testId(html, 'reserve-percent')?.textContent)).toBe(
        '2,5% das despesas dos grupos marcados como base',
      );
    });

    it('avisa que nao ha fundo de reserva quando o percentual e zero', async () => {
      const { html } = await setup({ reserveFundPercent: 0 });

      expect(plain(testId(html, 'reserve-percent')?.textContent)).toBe(
        'Sem fundo de reserva no recibo',
      );
    });

    it('muda o percentual pelo dialogo', async () => {
      const { fixture, html, http } = await setup();

      click(testId(html, 'edit-reserve-percent'));
      await settle(fixture);
      typeIn('urban-reserve-percent-dialog', 'percent', '2,5');
      submitDialog('urban-reserve-percent-dialog');
      await flushPromises();
      const put = http.expectOne({ method: 'PUT', url: SETTINGS_URL });
      expect(put.request.body).toEqual({ reserveFundPercent: 2.5 });
      put.flush({ reserveFundPercent: 2.5 });
      await flushPromises();
      answerFetch(http, { reserveFundPercent: 2.5 });
      await settle(fixture);

      expect(dialog('urban-reserve-percent-dialog')).toBeNull();
      expect(plain(testId(html, 'notice')?.textContent)).toBe(
        'Percentual do fundo de reserva atualizado',
      );
      expect(plain(testId(html, 'reserve-percent')?.textContent)).toContain('2,5%');
    });
  });

  describe('tarifas', () => {
    it('cadastra uma tarifa pelo dialogo, no mes de hoje', async () => {
      const { fixture, html, http } = await setup({ tariffs: [OLD] });

      click(testId(html, 'new-tariff'));
      await settle(fixture);
      typeInWater('5,5');
      submitDialog('urban-tariff-dialog');
      await flushPromises();
      const put = http.expectOne({ method: 'PUT', url: `${TARIFFS_URL}/2026-11` });
      expect(put.request.body).toEqual({
        water: [{ upToCubicMeters: null, fixedAmount: 0, pricePerCubicMeter: 5.5 }],
        sewage: null,
        sewagePercent: 100,
        gas: null,
      });
      put.flush(consumptionTariff({ validFrom: '2026-11' }));
      await flushPromises();
      answerFetch(http, { tariffs: [CURRENT, OLD] });
      await settle(fixture);

      expect(dialog('urban-tariff-dialog')).toBeNull();
      expect(plain(testId(html, 'notice')?.textContent)).toBe('Tarifa salva');
      expect(items(html)).toHaveLength(2);
    });

    it('edita uma tarifa no proprio mes', async () => {
      const { fixture, html, http } = await setup();

      click(button(html, 'Editar tarifa de março de 2026'));
      await settle(fixture);
      expect(dialog('urban-tariff-dialog')?.querySelector('h2')?.textContent).toContain(
        'Editar tarifa de março de 2026',
      );
      submitDialog('urban-tariff-dialog');
      await flushPromises();
      const put = http.expectOne({ method: 'PUT', url: `${TARIFFS_URL}/2026-03` });
      put.flush(OLD);
      await flushPromises();
      answerFetch(http);
      await settle(fixture);

      expect(dialog('urban-tariff-dialog')).toBeNull();
      expect(plain(testId(html, 'notice')?.textContent)).toBe('Tarifa salva');
    });

    it('explica o erro da API sem fechar o dialogo', async () => {
      const { fixture, html, http } = await setup({ tariffs: [OLD] });

      click(testId(html, 'new-tariff'));
      await settle(fixture);
      typeInWater('5,5');
      submitDialog('urban-tariff-dialog');
      await flushPromises();
      http
        .expectOne({ method: 'PUT', url: `${TARIFFS_URL}/2026-11` })
        .flush(problem(400, 'consumption.bands_invalid'), { status: 400, statusText: 'Erro' });
      await settle(fixture);

      expect(dialog('urban-tariff-dialog')?.querySelector('[role="alert"]')?.textContent).toContain(
        'As faixas precisam ser de 1 a 6, com limites crescentes, a última sem limite',
      );
      expect(testId(html, 'notice')).toBeNull();
    });

    it('apaga uma tarifa depois de confirmar', async () => {
      const { fixture, html, http } = await setup();

      click(button(html, 'Apagar tarifa de março de 2026'));
      await settle(fixture);
      submitDialog('urban-delete-tariff-dialog');
      await flushPromises();
      http
        .expectOne({ method: 'DELETE', url: `${TARIFFS_URL}/2026-03` })
        .flush(null, { status: 204, statusText: 'No Content' });
      await flushPromises();
      answerFetch(http, { tariffs: [UPCOMING, CURRENT] });
      await settle(fixture);

      expect(dialog('urban-delete-tariff-dialog')).toBeNull();
      expect(plain(testId(html, 'notice')?.textContent)).toBe('Tarifa apagada');
      expect(items(html)).toHaveLength(2);
    });

    it('avisa e atualiza a lista quando a tarifa ja nao existe', async () => {
      const { fixture, html, http } = await setup();

      click(button(html, 'Apagar tarifa de março de 2026'));
      await settle(fixture);
      submitDialog('urban-delete-tariff-dialog');
      await flushPromises();
      http
        .expectOne({ method: 'DELETE', url: `${TARIFFS_URL}/2026-03` })
        .flush(problem(404, 'consumption.tariff_not_found'), {
          status: 404,
          statusText: 'Não encontrado',
        });
      await flushPromises();
      answerFetch(http, { tariffs: [UPCOMING, CURRENT] });
      await settle(fixture);

      expect(
        dialog('urban-delete-tariff-dialog')?.querySelector('[role="alert"]')?.textContent,
      ).toContain('Esta tarifa não existe mais. Feche esta janela e confira a tela');
      expect(items(html)).toHaveLength(2);
    });

    it('apaga o aviso anterior ao abrir outro dialogo', async () => {
      const { fixture, html, http } = await setup({ tariffs: [OLD] });
      click(testId(html, 'new-tariff'));
      await settle(fixture);
      typeInWater('5,5');
      submitDialog('urban-tariff-dialog');
      await flushPromises();
      http.expectOne({ method: 'PUT', url: `${TARIFFS_URL}/2026-11` }).flush({});
      await flushPromises();
      answerFetch(http, { tariffs: [CURRENT, OLD] });
      await settle(fixture);
      expect(testId(html, 'notice')).not.toBeNull();

      click(testId(html, 'edit-reserve-percent'));
      await settle(fixture);

      expect(testId(html, 'notice')).toBeNull();
    });
  });
});
