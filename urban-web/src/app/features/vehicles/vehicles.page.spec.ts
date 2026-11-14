import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { jest } from '@jest/globals';
import { SessionStore } from '../../core/auth/session.store';
import { provideUrbanIcons } from '../../core/theme/icons';
import { Unit } from '../../core/units/units.models';
import { Vehicle } from '../../core/vehicles/vehicles.models';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, problem, signInAs } from '../../testing/auth.fixtures';
import {
  alertOf,
  chooseIn,
  dialogOf,
  removeOverlays,
  submitDialog,
  typeIn,
} from '../../testing/dialog.harness';
import { chooseMenuOption, menuSelectOptions, menuSelectValue } from '../../testing/menu-select';
import { unit, unitPage } from '../../testing/units.fixtures';
import { vehicle, vehiclePage } from '../../testing/vehicles.fixtures';
import { VehiclesPage } from './vehicles.page';

const TAG = 'urban-vehicle-dialog';

const UNIT_A = unit({ block: 'A', number: '101' });
const UNIT_B = unit({ block: 'B', number: '202' });
const FIRST = vehicle({
  unitId: UNIT_A.id,
  unitBlock: 'A',
  unitNumber: '101',
  plate: 'ABC1D23',
  model: 'Fiat Argo',
  color: 'Prata',
  version: '41',
});
const SECOND = vehicle({
  unitId: UNIT_B.id,
  unitBlock: null,
  unitNumber: '202',
  plate: 'XYZ9876',
  model: 'Honda CG 160',
  color: 'Vermelha',
});

interface Options {
  items?: Vehicle[];
  total?: number;
  units?: Unit[];
}

interface Harness {
  fixture: ComponentFixture<VehiclesPage>;
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
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  await signInAs(TestBed.inject(SessionStore), http, REGISTERED_MANAGER);
  const fixture = TestBed.createComponent(VehiclesPage);
  fixture.detectChanges();
  if (options !== 'pending') {
    const items = options.items ?? [FIRST, SECOND];
    listRequest(http).flush(vehiclePage(items, options.total ?? items.length));
    unitsRequest(http).flush(unitPage(options.units ?? [UNIT_A, UNIT_B]));
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

function listRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((request) => request.url === '/api/vehicles' && request.method === 'GET');
}

function unitsRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((request) => request.url === '/api/units');
}

function params(request: TestRequest): URLSearchParams {
  return new URLSearchParams(request.request.params.toString());
}

function testId(html: HTMLElement, id: string): HTMLElement | null {
  return html.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function text(html: HTMLElement, id: string): string {
  return testId(html, id)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

function rows(html: HTMLElement): HTMLElement[] {
  return [...html.querySelectorAll<HTMLElement>('[data-testid="vehicle-row"]')];
}

function rowText(row: HTMLElement): string {
  return row.textContent!.replace(/\s+/g, ' ').trim();
}

function button(root: ParentNode, label: string): HTMLButtonElement {
  return root.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`)!;
}

function failure(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

function choose(html: HTMLElement, id: string, value: string): void {
  const field = testId(html, id)!;
  if (field.tagName.toLowerCase() === 'urban-menu-select') {
    chooseMenuOption(field, value);
    return;
  }
  (field as HTMLInputElement).value = value;
  field.dispatchEvent(new Event('change'));
}

/** Responde à atualização que a tela faz depois de gravar: só a lista (as unidades não mudam). */
function answerRefresh(http: HttpTestingController, items: Vehicle[] = [FIRST, SECOND]): void {
  listRequest(http).flush(vehiclePage(items));
}

describe('VehiclesPage', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    removeOverlays();
    TestBed.inject(HttpTestingController).match(() => true);
  });

  describe('ao abrir', () => {
    it('mostra que está carregando enquanto a lista não chega', async () => {
      const { html } = await setup('pending');

      expect(html.querySelector('h1')?.textContent?.trim()).toBe('Veículos');
      expect(testId(html, 'loading')?.getAttribute('role')).toBe('status');
      expect(text(html, 'loading')).toBe('Carregando os veículos');
      expect(testId(html, 'vehicles-table')).toBeNull();
    });

    it('pede a primeira página de veículos e as unidades ativas', async () => {
      const { http, fixture } = await setup('pending');

      expect(params(listRequest(http)).toString()).toBe('page=1&pageSize=50');
      expect(params(unitsRequest(http)).toString()).toBe('status=active&page=1&pageSize=200');
      expect(fixture).toBeTruthy();
    });

    it('mostra o erro e deixa tentar de novo quando a busca falha', async () => {
      const { http, html, fixture } = await setup('pending');
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      unitsRequest(http).flush(unitPage([UNIT_A]));
      await settle(fixture);

      expect(text(html, 'load-error')).toContain('Não foi possível carregar os veículos');

      testId(html, 'retry')!.click();
      listRequest(http).flush(vehiclePage([FIRST]));
      unitsRequest(http).flush(unitPage([UNIT_A]));
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(rows(html)).toHaveLength(1);
    });
  });

  describe('lista', () => {
    it('mostra uma linha por veículo, com placa, modelo, cor e unidade', async () => {
      const { html } = await setup();

      const [first, second] = rows(html);
      expect(rows(html)).toHaveLength(2);
      expect(rowText(first)).toContain('ABC1D23');
      expect(rowText(first)).toContain('Fiat Argo');
      expect(rowText(first)).toContain('Prata');
      expect(rowText(first)).toContain('Bloco A · 101');
      expect(rowText(second)).toContain('XYZ9876');
      expect(rowText(second)).toContain('202');
      expect(rowText(second)).not.toContain('Bloco');
    });

    it('descreve a tabela para leitores de tela', async () => {
      const { html } = await setup();

      expect(html.querySelector('table caption')?.textContent?.trim()).toBe('Lista de veículos');
      const headers = [...html.querySelectorAll('th')].map((header) => header.textContent!.trim());
      expect(headers).toEqual(['Placa', 'Veículo', 'Unidade', 'Ações']);
      expect(html.querySelectorAll('th[scope="col"]')).toHaveLength(4);
    });

    it('cada veículo pode ser editado e excluído, com o nome no rótulo do botão', async () => {
      const { html } = await setup();

      const [row] = rows(html);
      expect(button(row, 'Editar veículo ABC1D23')).not.toBeNull();
      expect(button(row, 'Excluir veículo ABC1D23')).not.toBeNull();
    });

    it('conta os veículos no plural e no singular', async () => {
      const many = await setup({ items: [FIRST], total: 120 });
      expect(text(many.html, 'total')).toBe('120 veículos');
      TestBed.resetTestingModule();

      const one = await setup({ items: [FIRST], total: 1 });
      expect(text(one.html, 'total')).toBe('1 veículo');
    });

    it('sem nenhum veículo, convida a cadastrar', async () => {
      const { html } = await setup({ items: [], total: 0 });

      expect(text(html, 'empty')).toContain('Nenhum veículo cadastrado ainda');
      expect(testId(html, 'vehicles-table')).toBeNull();
      expect(testId(html, 'no-results')).toBeNull();
      expect(testId(html, 'new-vehicle')).not.toBeNull();
    });

    it('sem unidade ativa, explica e leva ao cadastro de unidades, sem oferecer o cadastro', async () => {
      const { html } = await setup({ items: [], total: 0, units: [] });

      expect(text(html, 'no-units')).toContain('Cadastre uma unidade');
      expect(testId(html, 'no-units-link')?.getAttribute('href')).toBe('/unidades');
      expect(testId(html, 'new-vehicle')).toBeNull();
      expect(testId(html, 'empty')).toBeNull();
    });
  });

  describe('filtros', () => {
    it('oferece a unidade, com "Todas as unidades" no começo, e a busca', async () => {
      const { html } = await setup();

      const unitFilter = testId(html, 'filter-unit')!;
      expect(menuSelectOptions(unitFilter).map((option) => option.label)).toEqual([
        'Todas as unidades',
        'Bloco A · 101',
        'Bloco B · 202',
      ]);
      expect(menuSelectValue(unitFilter)).toBe('');
      expect(testId(html, 'filter-search')).not.toBeNull();
    });

    it.each([
      ['filter-unit', UNIT_B.id, 'unitId', UNIT_B.id],
      ['filter-search', 'argo', 'search', 'argo'],
    ])('mexer em %s busca de novo a partir da página 1', async (id, value, key, sent) => {
      const { http, html, fixture } = await setup({ items: [FIRST], total: 120 });

      choose(html, id, value);
      const request = listRequest(http);
      request.flush(vehiclePage([FIRST], 1));
      await settle(fixture);

      expect(params(request).get(key)).toBe(sent);
      expect(params(request).get('page')).toBe('1');
      http.expectNone('/api/units');
    });

    it('sem resultado com filtro, avisa e oferece limpar os filtros', async () => {
      const { http, html, fixture } = await setup();

      choose(html, 'filter-search', 'zzz');
      listRequest(http).flush(vehiclePage([], 0));
      await settle(fixture);

      expect(text(html, 'no-results')).toContain('Nenhum veículo encontrado com estes filtros');
      expect(testId(html, 'empty')).toBeNull();

      testId(html, 'clear-filters')!.click();
      const request = listRequest(http);
      request.flush(vehiclePage([FIRST, SECOND]));
      await settle(fixture);

      expect(params(request).toString()).toBe('page=1&pageSize=50');
      expect((testId(html, 'filter-search') as HTMLInputElement).value).toBe('');
      expect(testId(html, 'clear-filters')).toBeNull();
    });
  });

  describe('paginação', () => {
    it('some quando cabe tudo numa página', async () => {
      const { html } = await setup();

      expect(testId(html, 'pagination')).toBeNull();
    });

    it('mostra a página atual e navega para a próxima e para a anterior', async () => {
      const { http, html, fixture } = await setup({ items: [FIRST], total: 120 });

      expect(text(html, 'page-status')).toBe('Página 1 de 3');
      expect((testId(html, 'previous') as HTMLButtonElement).disabled).toBe(true);

      testId(html, 'next')!.click();
      const next = listRequest(http);
      next.flush(vehiclePage([SECOND], 120, 2));
      await settle(fixture);
      expect(params(next).get('page')).toBe('2');
      expect(text(html, 'page-status')).toBe('Página 2 de 3');

      testId(html, 'previous')!.click();
      const previous = listRequest(http);
      previous.flush(vehiclePage([FIRST], 120, 1));
      await settle(fixture);
      expect(params(previous).get('page')).toBe('1');
    });
  });

  describe('novo veículo', () => {
    it('abre o diálogo, grava e atualiza a lista, avisando o que foi feito', async () => {
      const { http, html, fixture } = await setup();

      testId(html, 'new-vehicle')!.click();
      TestBed.tick();
      expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Novo veículo');
      chooseIn(TAG, 'unitId', UNIT_A.id);
      typeIn(TAG, 'plate', 'def2g34');
      typeIn(TAG, 'model', 'VW Gol');
      typeIn(TAG, 'color', 'Branco');
      submitDialog(TAG);
      await settle(fixture);

      const post = http.expectOne(
        (request) => request.method === 'POST' && request.url === '/api/vehicles',
      );
      expect(post.request.body).toEqual({
        unitId: UNIT_A.id,
        plate: 'DEF2G34',
        model: 'VW Gol',
        color: 'Branco',
      });
      post.flush(vehicle());
      await settle(fixture);
      answerRefresh(http, [FIRST, SECOND, vehicle({ plate: 'DEF2G34' })]);
      await settle(fixture);

      expect(document.querySelector(TAG)).toBeNull();
      expect(rows(html)).toHaveLength(3);
      expect(text(html, 'notice')).toBe('Veículo DEF2G34 cadastrado');
    });

    it('já vem com a unidade que a lista está filtrando', async () => {
      const { http, html, fixture } = await setup();
      choose(html, 'filter-unit', UNIT_B.id);
      listRequest(http).flush(vehiclePage([SECOND]));
      await settle(fixture);

      testId(html, 'new-vehicle')!.click();
      TestBed.tick();

      const field = dialogOf(TAG).querySelector<HTMLElement>('[formControlName="unitId"]')!;
      expect(menuSelectValue(field)).toBe(UNIT_B.id);
    });

    it('explica no diálogo por que a API recusou, sem fechar', async () => {
      const { http, html, fixture } = await setup();
      testId(html, 'new-vehicle')!.click();
      TestBed.tick();
      chooseIn(TAG, 'unitId', UNIT_A.id);
      typeIn(TAG, 'plate', 'ABC1D23');
      typeIn(TAG, 'model', 'Fiat Argo');
      typeIn(TAG, 'color', 'Prata');

      submitDialog(TAG);
      await settle(fixture);
      const [body, options] = failure(409, 'vehicle.plate.already_registered');
      http.expectOne('/api/vehicles').flush(body, options);
      await settle(fixture);

      expect(alertOf(TAG)).toBe('Já existe um veículo com esta placa neste condomínio');
      expect(document.querySelector(TAG)).not.toBeNull();
    });
  });

  describe('editar veículo', () => {
    it('abre com os dados do veículo e grava com a versão lida', async () => {
      const { http, html, fixture } = await setup();

      button(rows(html)[0], 'Editar veículo ABC1D23').click();
      TestBed.tick();
      expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Editar veículo');
      typeIn(TAG, 'color', 'Preto');
      submitDialog(TAG);
      await settle(fixture);

      const put = http.expectOne(`/api/vehicles/${FIRST.id}`);
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual({
        unitId: UNIT_A.id,
        plate: 'ABC1D23',
        model: 'Fiat Argo',
        color: 'Preto',
        version: '41',
      });
      put.flush({ ...FIRST, color: 'Preto', version: '42' });
      await settle(fixture);
      answerRefresh(http, [{ ...FIRST, color: 'Preto', version: '42' }, SECOND]);
      await settle(fixture);

      expect(document.querySelector(TAG)).toBeNull();
      expect(rowText(rows(html)[0])).toContain('Preto');
      expect(text(html, 'notice')).toBe('Veículo ABC1D23 atualizado');
    });

    it('quando outra pessoa mexeu antes, avisa no diálogo e atualiza a lista por trás', async () => {
      const { http, html, fixture } = await setup();
      button(rows(html)[0], 'Editar veículo ABC1D23').click();
      TestBed.tick();
      typeIn(TAG, 'color', 'Preto');

      submitDialog(TAG);
      await settle(fixture);
      const [body, options] = failure(409, 'concurrency.conflict');
      http.expectOne(`/api/vehicles/${FIRST.id}`).flush(body, options);
      await settle(fixture);
      answerRefresh(http, [{ ...FIRST, color: 'Azul', version: '43' }, SECOND]);
      await settle(fixture);

      expect(alertOf(TAG)).toBe(
        'Este veículo foi alterado em outro lugar. Feche esta janela e confira a lista, que já foi atualizada',
      );
      expect(rowText(rows(html)[0])).toContain('Azul');
    });

    it('quando a unidade escolhida foi desativada, avisa e atualiza as unidades', async () => {
      const { http, html, fixture } = await setup();
      button(rows(html)[0], 'Editar veículo ABC1D23').click();
      TestBed.tick();
      chooseIn(TAG, 'unitId', UNIT_B.id);

      submitDialog(TAG);
      await settle(fixture);
      const [body, options] = failure(409, 'unit.deactivated');
      http.expectOne(`/api/vehicles/${FIRST.id}`).flush(body, options);
      await settle(fixture);
      answerRefresh(http);
      await settle(fixture);
      unitsRequest(http).flush(unitPage([UNIT_A]));
      await settle(fixture);

      expect(alertOf(TAG)).toBe(
        'Esta unidade foi desativada. Feche esta janela e escolha outra unidade',
      );
    });
  });

  describe('excluir veículo', () => {
    it('pede confirmação na própria linha e explica que é definitivo', async () => {
      const { html, fixture } = await setup();

      button(rows(html)[0], 'Excluir veículo ABC1D23').click();
      await settle(fixture);

      const confirm = text(html, 'delete-confirm');
      expect(confirm).toContain('Excluir o veículo ABC1D23?');
      expect(confirm).toContain('definitiva');
      expect(testId(html, 'delete-confirm')?.getAttribute('role')).toBe('alertdialog');
    });

    it('cancelar fecha a confirmação sem chamar a API', async () => {
      const { http, html, fixture } = await setup();
      button(rows(html)[0], 'Excluir veículo ABC1D23').click();
      await settle(fixture);

      testId(html, 'cancel-delete')!.click();
      await settle(fixture);

      expect(testId(html, 'delete-confirm')).toBeNull();
      http.expectNone((request) => request.method === 'DELETE');
    });

    it('só uma confirmação por vez: abrir outra fecha a primeira', async () => {
      const { html, fixture } = await setup();

      button(rows(html)[0], 'Excluir veículo ABC1D23').click();
      await settle(fixture);
      button(rows(html)[1], 'Excluir veículo XYZ9876').click();
      await settle(fixture);

      expect(html.querySelectorAll('[data-testid="delete-confirm"]')).toHaveLength(1);
      expect(text(html, 'delete-confirm')).toContain('XYZ9876');
    });

    it('confirmar exclui, atualiza a lista e avisa', async () => {
      const { http, html, fixture } = await setup();
      button(rows(html)[0], 'Excluir veículo ABC1D23').click();
      await settle(fixture);

      testId(html, 'confirm-delete')!.click();
      await settle(fixture);
      const request = http.expectOne(`/api/vehicles/${FIRST.id}`);
      expect(request.request.method).toBe('DELETE');
      request.flush(null, { status: 204, statusText: 'No Content' });
      await settle(fixture);
      answerRefresh(http, [SECOND]);
      await settle(fixture);

      expect(rows(html)).toHaveLength(1);
      expect(testId(html, 'delete-confirm')).toBeNull();
      expect(text(html, 'notice')).toBe('Veículo ABC1D23 excluído');
    });

    it('veículo que já não existe: explica e atualiza a lista', async () => {
      const { http, html, fixture } = await setup();
      button(rows(html)[0], 'Excluir veículo ABC1D23').click();
      await settle(fixture);

      testId(html, 'confirm-delete')!.click();
      await settle(fixture);
      const [body, options] = failure(404, 'vehicle.not_found');
      http.expectOne(`/api/vehicles/${FIRST.id}`).flush(body, options);
      await settle(fixture);
      answerRefresh(http, [SECOND]);
      await settle(fixture);

      expect(text(html, 'action-error')).toBe(
        'Este veículo não existe mais. A lista já foi atualizada',
      );
      expect(rows(html)).toHaveLength(1);
      expect(testId(html, 'delete-confirm')).toBeNull();
    });

    it('falha de rede ao excluir explica e mantém a linha', async () => {
      const { http, html, fixture } = await setup();
      button(rows(html)[0], 'Excluir veículo ABC1D23').click();
      await settle(fixture);

      testId(html, 'confirm-delete')!.click();
      await settle(fixture);
      http.expectOne(`/api/vehicles/${FIRST.id}`).error(new ProgressEvent('error'));
      await settle(fixture);

      expect(text(html, 'action-error')).toContain('Não foi possível conectar ao Urban');
      expect(rows(html)).toHaveLength(2);
    });
  });
});
