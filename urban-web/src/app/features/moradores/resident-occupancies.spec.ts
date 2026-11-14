import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { Component, inject } from '@angular/core';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { Resident } from '../../core/residents/residents.models';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { fieldValueOf, menuSelectOptions, setFieldValue } from '../../testing/menu-select';
import { occupancy, resident } from '../../testing/residents.fixtures';
import { unit, unitPage } from '../../testing/units.fixtures';
import { ResidentOccupancies } from './resident-occupancies';
import { ResidentFormStore } from './resident-form.store';

const A_101 = unit({ id: '0199aaaa-0000-7000-8000-000000000001', block: 'A', number: '101' });
const B_202 = unit({ block: 'B', number: '202' });
const HOUSE_7 = unit({ block: null, number: '7', type: 'house' });

const CURRENT = occupancy({
  id: 'occ-current',
  unitId: A_101.id,
  unitBlock: 'A',
  unitNumber: '101',
});
const PAST = occupancy({
  id: 'occ-past',
  unitId: B_202.id,
  unitBlock: 'B',
  unitNumber: '202',
  kind: 'tenant',
  startDate: '2023-01-10',
  endDate: '2026-10-31',
});

@Component({
  imports: [ResidentOccupancies],
  providers: [ResidentFormStore],
  template: '<urban-resident-occupancies />',
})
class Host {
  readonly store = inject(ResidentFormStore);
}

interface Harness {
  fixture: ComponentFixture<Host>;
  http: HttpTestingController;
  html: HTMLElement;
  found: Resident;
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    await flushPromises();
    TestBed.tick();
    fixture.detectChanges();
    await fixture.whenStable();
  }
}

async function open(
  found: Resident = resident({ occupancies: [CURRENT, PAST] }),
): Promise<Harness> {
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
  const fixture = TestBed.createComponent(Host);
  fixture.detectChanges();
  void fixture.componentInstance.store.loadForEdit(found.id);
  http.expectOne(`/api/residents/${found.id}`).flush(found);
  await settle(fixture);
  return { fixture, http, html: fixture.nativeElement, found };
}

function testId(html: HTMLElement, id: string): HTMLElement | null {
  return html.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function click(html: HTMLElement, id: string): void {
  testId(html, id)!.click();
}

function field(html: HTMLElement, name: string): HTMLElement {
  return html.querySelector<HTMLElement>(`[formControlName="${name}"]`)!;
}

function text(html: HTMLElement, id: string): string {
  return (testId(html, id)?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

function errorTexts(html: HTMLElement): string[] {
  return [...html.querySelectorAll('mat-error')].map((error) => error.textContent!.trim());
}

function alertTexts(html: HTMLElement): string[] {
  return [...html.querySelectorAll('[role="alert"]')].map((alert) => alert.textContent!.trim());
}

async function submit(h: Harness, formId: string): Promise<void> {
  testId(h.html, formId)!.dispatchEvent(new Event('submit'));
  await settle(h.fixture);
}

function unitsRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((request) => request.url === '/api/units' && request.method === 'GET');
}

function reject(request: TestRequest, status: number, code: string): void {
  request.flush({ code }, { status, statusText: 'Erro' });
}

describe('ResidentOccupancies', () => {
  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    http.match((request) => request.url.startsWith('icons/'));
    http.verify();
  });

  describe('lista', () => {
    it('mostra cada vínculo com a unidade, o tipo e o período, os em andamento e os encerrados', async () => {
      const { html } = await open();

      const items = [...html.querySelectorAll('[data-testid="occupancy"]')].map((item) =>
        item.textContent!.replace(/\s+/g, ' ').trim(),
      );

      expect(items).toHaveLength(2);
      expect(items[0]).toContain('Bloco A · 101');
      expect(items[0]).toContain('Proprietário, desde 01/03/2024');
      expect(items[0]).toContain('Mora aqui');
      expect(items[1]).toContain('Bloco B · 202');
      expect(items[1]).toContain('Inquilino, de 10/01/2023 a 31/10/2026');
      expect(items[1]).toContain('Já saiu');
    });

    it('só o vínculo em andamento tem "Encerrar vínculo", com o nome da unidade para o leitor de tela', async () => {
      const { html } = await open();

      const buttons = html.querySelectorAll('[data-testid="end-occupancy"]');

      expect(buttons).toHaveLength(1);
      expect(buttons[0].getAttribute('aria-label')).toBe('Encerrar vínculo em Bloco A · 101');
    });

    it('morador sem nenhum vínculo explica que ainda não mora em unidade', async () => {
      const { html } = await open(resident({ occupancies: [] }));

      expect(testId(html, 'no-occupancies')).not.toBeNull();
    });
  });

  describe('encerrar vínculo', () => {
    it('abre o formulário da saída sem chamar a API', async () => {
      const h = await open();

      click(h.html, 'end-occupancy');
      await settle(h.fixture);

      expect(testId(h.html, 'end-form')).not.toBeNull();
      expect(testId(h.html, 'end-occupancy')).toBeNull();
      expect(fieldValueOf(field(h.html, 'endDate'))).toBe('');
    });

    it('cancelar fecha o formulário e não chama a API', async () => {
      const h = await open();
      click(h.html, 'end-occupancy');
      await settle(h.fixture);

      click(h.html, 'cancel-end');
      await settle(h.fixture);

      expect(testId(h.html, 'end-form')).toBeNull();
      expect(testId(h.html, 'end-occupancy')).not.toBeNull();
    });

    it('sem data não chama a API e pede a data', async () => {
      const h = await open();
      click(h.html, 'end-occupancy');
      await settle(h.fixture);

      await submit(h, 'end-form');

      expect(errorTexts(h.html)).toEqual(['Informe a data de saída']);
    });

    it.each([
      ['29/02/2024', 'A saída não pode ser antes da entrada, em 01/03/2024'],
      ['01/01/2099', 'A saída não pode ser no futuro'],
      ['31/02/2026', 'Informe uma data válida, como 31/01/2026'],
    ])('recusa a data %s sem chamar a API', async (date, message) => {
      const h = await open();
      click(h.html, 'end-occupancy');
      await settle(h.fixture);
      setFieldValue(field(h.html, 'endDate'), date.replace(/\D/g, ''));

      await submit(h, 'end-form');

      expect(errorTexts(h.html)).toEqual([message]);
    });

    it('registra a saída, atualiza a lista, fecha o formulário e avisa', async () => {
      const h = await open();
      click(h.html, 'end-occupancy');
      await settle(h.fixture);
      setFieldValue(field(h.html, 'endDate'), '01042024');

      await submit(h, 'end-form');
      const request = h.http.expectOne(`/api/residents/${h.found.id}/occupancies/occ-current/end`);
      expect(request.request.body).toEqual({ endDate: '2024-04-01' });
      request.flush({
        ...h.found,
        occupancies: [{ ...CURRENT, endDate: '2024-04-01' }, PAST],
      });
      await settle(h.fixture);

      expect(testId(h.html, 'end-form')).toBeNull();
      expect(testId(h.html, 'end-occupancy')).toBeNull();
      expect(text(h.html, 'occupancy-notice')).toBe('Saída de Bloco A · 101 registrada');
      expect(text(h.html, 'occupancies')).toContain('Proprietário, de 01/03/2024 a 01/04/2024');
    });

    it('mostra o motivo da recusa da API e mantém o formulário com o que foi digitado', async () => {
      const h = await open();
      click(h.html, 'end-occupancy');
      await settle(h.fixture);
      setFieldValue(field(h.html, 'endDate'), '01042024');

      await submit(h, 'end-form');
      reject(
        h.http.expectOne(`/api/residents/${h.found.id}/occupancies/occ-current/end`),
        400,
        'condominium.occupancy.end_date_invalid',
      );
      await settle(h.fixture);

      expect(alertTexts(h.html).join(' ')).toContain(
        'A data de saída deve ficar entre a entrada e hoje',
      );
      expect(testId(h.html, 'end-form')).not.toBeNull();
      expect(fieldValueOf(field(h.html, 'endDate'))).toBe('01/04/2024');
    });

    it('vínculo que já terminou em outra tela: explica e busca o morador de novo', async () => {
      const h = await open();
      click(h.html, 'end-occupancy');
      await settle(h.fixture);
      setFieldValue(field(h.html, 'endDate'), '01042024');

      await submit(h, 'end-form');
      reject(
        h.http.expectOne(`/api/residents/${h.found.id}/occupancies/occ-current/end`),
        409,
        'condominium.occupancy.already_ended',
      );
      await settle(h.fixture);
      h.http.expectOne(`/api/residents/${h.found.id}`).flush({
        ...h.found,
        occupancies: [{ ...CURRENT, endDate: '2026-11-01' }, PAST],
      });
      await settle(h.fixture);

      expect(alertTexts(h.html).join(' ')).toContain('já foi encerrado');
      expect(testId(h.html, 'end-occupancy')).toBeNull();
    });

    it('trava os botões enquanto grava', async () => {
      const h = await open();
      click(h.html, 'end-occupancy');
      await settle(h.fixture);
      setFieldValue(field(h.html, 'endDate'), '01042024');

      await submit(h, 'end-form');

      expect(testId(h.html, 'cancel-end')!.hasAttribute('disabled')).toBe(true);
      expect(h.html.querySelector('[data-testid="end-form"] button[type="submit"]')).toHaveProperty(
        'disabled',
        true,
      );
      h.http.expectOne(`/api/residents/${h.found.id}/occupancies/occ-current/end`);
    });
  });

  describe('adicionar unidade', () => {
    async function openAdd(h: Harness, units = [A_101, B_202, HOUSE_7]): Promise<void> {
      click(h.html, 'add-occupancy');
      await settle(h.fixture);
      unitsRequest(h.http).flush(unitPage(units, units.length, 1, 200));
      await settle(h.fixture);
    }

    it('busca as unidades ao abrir, mostrando "Carregando as unidades" enquanto espera', async () => {
      const h = await open();

      click(h.html, 'add-occupancy');
      await settle(h.fixture);

      expect(text(h.html, 'units-loading')).toBe('Carregando as unidades');
      expect(testId(h.html, 'add-occupancy')).toBeNull();
      unitsRequest(h.http).flush(unitPage([A_101], 1, 1, 200));
      await settle(h.fixture);
      expect(testId(h.html, 'units-loading')).toBeNull();
    });

    it('oferece só as unidades onde o morador não mora hoje (as que ele já deixou valem)', async () => {
      const h = await open();

      await openAdd(h);

      expect(menuSelectOptions(field(h.html, 'unitId')).map((option) => option.label)).toEqual([
        'Bloco B · 202',
        '7',
      ]);
      expect(menuSelectOptions(field(h.html, 'kind')).map((option) => option.label)).toEqual([
        'Proprietário',
        'Inquilino',
        'Dependente',
      ]);
    });

    it('sem unidade livre explica e não mostra o formulário', async () => {
      const h = await open();

      await openAdd(h, [A_101]);

      expect(text(h.html, 'no-free-units')).toContain('já mora em todas as unidades ativas');
      expect(testId(h.html, 'add-form')).toBeNull();
    });

    it('falha na busca das unidades avisa e "Tentar de novo" busca outra vez', async () => {
      const h = await open();
      click(h.html, 'add-occupancy');
      await settle(h.fixture);
      reject(unitsRequest(h.http), 500, 'x');
      await settle(h.fixture);

      expect(testId(h.html, 'units-error')).not.toBeNull();
      click(h.html, 'retry-units');
      await settle(h.fixture);
      unitsRequest(h.http).flush(unitPage([A_101, B_202], 2, 1, 200));
      await settle(h.fixture);

      expect(testId(h.html, 'units-error')).toBeNull();
      expect(testId(h.html, 'add-form')).not.toBeNull();
    });

    it('sem preencher não chama a API e mostra o que falta', async () => {
      const h = await open();
      await openAdd(h);

      await submit(h, 'add-form');

      expect(errorTexts(h.html)).toEqual([
        'Escolha a unidade',
        'Escolha o vínculo: proprietário, inquilino ou dependente',
        'Informe a data de entrada',
      ]);
    });

    it('morador menor de idade: só o vínculo de dependente, os outros mostram o erro e não enviam', async () => {
      const minor = resident({
        birthDate: `${new Date().getFullYear() - 10}-04-10`,
        profession: null,
        emergencyContact: null,
        occupancies: [occupancy({ unitId: A_101.id, kind: 'dependent' })],
      });
      const h = await open(minor);
      await openAdd(h);
      setFieldValue(field(h.html, 'unitId'), B_202.id);
      setFieldValue(field(h.html, 'kind'), 'tenant');
      setFieldValue(field(h.html, 'startDate'), '05012026');

      await submit(h, 'add-form');

      expect(errorTexts(h.html)).toEqual(['Quem tem menos de 18 anos só pode ser dependente']);
      // Nenhuma gravação saiu: o verify() do afterEach falharia.
    });

    it('morador menor de idade pode ganhar outra unidade como dependente', async () => {
      const minor = resident({
        birthDate: `${new Date().getFullYear() - 10}-04-10`,
        profession: null,
        emergencyContact: null,
        occupancies: [occupancy({ unitId: A_101.id, kind: 'dependent' })],
      });
      const h = await open(minor);
      await openAdd(h);
      setFieldValue(field(h.html, 'unitId'), B_202.id);
      setFieldValue(field(h.html, 'kind'), 'dependent');
      setFieldValue(field(h.html, 'startDate'), '05012026');

      await submit(h, 'add-form');

      const request = h.http.expectOne(`/api/residents/${minor.id}/occupancies`);
      expect(request.request.body.kind).toBe('dependent');
      request.flush({ ...minor }, { status: 201, statusText: 'Created' });
      await settle(h.fixture);
    });

    it('adiciona o vínculo, atualiza a lista, fecha o formulário e avisa', async () => {
      const h = await open();
      await openAdd(h);
      setFieldValue(field(h.html, 'unitId'), HOUSE_7.id);
      setFieldValue(field(h.html, 'kind'), 'tenant');
      setFieldValue(field(h.html, 'startDate'), '05012026');

      await submit(h, 'add-form');
      const request = h.http.expectOne(`/api/residents/${h.found.id}/occupancies`);
      expect(request.request.body).toEqual({
        unitId: HOUSE_7.id,
        kind: 'tenant',
        startDate: '2026-01-05',
      });
      request.flush(
        {
          ...h.found,
          occupancies: [
            CURRENT,
            occupancy({
              unitId: HOUSE_7.id,
              unitBlock: null,
              unitNumber: '7',
              kind: 'tenant',
              startDate: '2026-01-05',
            }),
            PAST,
          ],
        },
        { status: 201, statusText: 'Created' },
      );
      await settle(h.fixture);

      expect(testId(h.html, 'add-form')).toBeNull();
      expect(testId(h.html, 'add-occupancy')).not.toBeNull();
      expect(text(h.html, 'occupancy-notice')).toBe('Vínculo com 7 adicionado');
      expect(h.html.querySelectorAll('[data-testid="occupancy"]')).toHaveLength(3);
    });

    it('já mora na unidade (tela velha): explica, mantém o formulário e busca o morador de novo', async () => {
      const h = await open();
      await openAdd(h);
      setFieldValue(field(h.html, 'unitId'), B_202.id);
      setFieldValue(field(h.html, 'kind'), 'owner');
      setFieldValue(field(h.html, 'startDate'), '05012026');

      await submit(h, 'add-form');
      reject(
        h.http.expectOne(`/api/residents/${h.found.id}/occupancies`),
        409,
        'condominium.occupancy.already_active',
      );
      await settle(h.fixture);
      h.http.expectOne(`/api/residents/${h.found.id}`).flush({
        ...h.found,
        occupancies: [CURRENT, occupancy({ unitId: B_202.id, unitBlock: 'B', unitNumber: '202' })],
      });
      await settle(h.fixture);

      expect(alertTexts(h.html).join(' ')).toContain('já mora nesta unidade');
      expect(testId(h.html, 'add-form')).not.toBeNull();
      expect(menuSelectOptions(field(h.html, 'unitId')).map((option) => option.label)).toEqual([
        '7',
      ]);
    });

    it('cancelar fecha o formulário e abrir de novo não busca as unidades outra vez', async () => {
      const h = await open();
      await openAdd(h);

      click(h.html, 'cancel-add');
      await settle(h.fixture);
      expect(testId(h.html, 'add-form')).toBeNull();
      click(h.html, 'add-occupancy');
      await settle(h.fixture);

      h.http.expectNone((request) => request.url === '/api/units');
      expect(testId(h.html, 'add-form')).not.toBeNull();
    });

    it('abrir um formulário fecha o outro', async () => {
      const h = await open();
      click(h.html, 'end-occupancy');
      await settle(h.fixture);

      click(h.html, 'add-occupancy');
      await settle(h.fixture);
      unitsRequest(h.http).flush(unitPage([A_101, B_202], 2, 1, 200));
      await settle(h.fixture);

      expect(testId(h.html, 'end-form')).toBeNull();
      expect(testId(h.html, 'add-form')).not.toBeNull();

      click(h.html, 'end-occupancy');
      await settle(h.fixture);

      expect(testId(h.html, 'add-form')).toBeNull();
      expect(testId(h.html, 'end-form')).not.toBeNull();
    });
  });
});
