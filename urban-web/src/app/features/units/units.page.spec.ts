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
import { Unit, UnitsSummary } from '../../core/units/units.models';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, problem, signInAs } from '../../testing/auth.fixtures';
import { unit, unitPage, unitsSummary } from '../../testing/units.fixtures';
import { UnitsPage } from './units.page';

interface Options {
  items?: Unit[];
  total?: number;
  summary?: UnitsSummary;
}

interface Harness {
  fixture: ComponentFixture<UnitsPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

const FIRST = unit({ block: 'A', number: '101', version: '41' });
const SECOND = unit({ block: 'A', number: '102', type: 'house', idealFraction: null, area: null });
const OFF = unit({
  block: 'B',
  number: '201',
  isActive: false,
  deactivatedAt: '2026-11-01T10:00:00Z',
});

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
  const fixture = TestBed.createComponent(UnitsPage);
  fixture.detectChanges();
  if (options !== 'pending') {
    const items = options.items ?? [FIRST, SECOND];
    listRequest(http).flush(unitPage(items, options.total ?? items.length));
    http.expectOne('/api/units/summary').flush(options.summary ?? unitsSummary());
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
  return http.expectOne((request) => request.url === '/api/units' && request.method === 'GET');
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
  return [...html.querySelectorAll<HTMLElement>('[data-testid="unit-row"]')];
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
  const select = testId(html, id) as HTMLSelectElement | HTMLInputElement;
  select.value = value;
  select.dispatchEvent(new Event('change'));
}

function dialog(): HTMLElement | null {
  return document.querySelector('urban-unit-dialog');
}

function typeInDialog(name: string, value: string): void {
  const input = dialog()!.querySelector<HTMLInputElement>(`[formControlName="${name}"]`)!;
  input.value = value;
  input.dispatchEvent(new Event('input'));
}

function submitDialog(): void {
  dialog()!.querySelector('form')!.dispatchEvent(new Event('submit'));
}

function dialogAlert(): string {
  return dialog()?.querySelector('[role="alert"]')?.textContent?.trim() ?? '';
}

/** Responde à atualização que a tela faz depois de gravar: a lista e o resumo. */
function answerRefresh(http: HttpTestingController, items: Unit[] = [FIRST, SECOND]): void {
  listRequest(http).flush(unitPage(items));
  http.expectOne('/api/units/summary').flush(unitsSummary());
}

describe('UnitsPage', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    document.querySelectorAll('.cdk-overlay-container').forEach((overlay) => overlay.remove());
    TestBed.inject(HttpTestingController).match(() => true);
  });

  describe('ao abrir', () => {
    it('mostra que esta carregando enquanto a lista nao chega', async () => {
      const { html } = await setup('pending');

      expect(html.querySelector('h1')?.textContent?.trim()).toBe('Unidades');
      expect(testId(html, 'loading')?.getAttribute('role')).toBe('status');
      expect(text(html, 'loading')).toBe('Carregando as unidades');
      expect(testId(html, 'units-table')).toBeNull();
    });

    it('pede as unidades ativas da primeira pagina, junto com o resumo', async () => {
      const { http, fixture } = await setup('pending');

      const request = listRequest(http);
      http.expectOne('/api/units/summary');

      expect(params(request).toString()).toBe('status=active&page=1&pageSize=50');
      expect(fixture).toBeTruthy();
    });

    it('oferece cadastrar uma unidade ou varias de uma vez', async () => {
      const { html } = await setup();

      expect(text(html, 'new-unit')).toBe('Nova unidade');
      const batch = testId(html, 'batch-link') as HTMLAnchorElement;
      expect(batch.textContent?.trim()).toBe('Cadastrar em lote');
      expect(batch.getAttribute('href')).toBe('/unidades/lote');
    });

    it('mostra o erro e deixa tentar de novo quando a busca falha', async () => {
      const { http, html, fixture } = await setup('pending');
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      http.expectOne('/api/units/summary').flush(unitsSummary());
      await settle(fixture);

      expect(text(html, 'load-error')).toContain('Não foi possível carregar as unidades');

      testId(html, 'retry')!.click();
      listRequest(http).flush(unitPage([FIRST]));
      http.expectOne('/api/units/summary').flush(unitsSummary());
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(rows(html)).toHaveLength(1);
    });
  });

  describe('resumo', () => {
    it('mostra as contagens, a fracao ocupada e o que falta preencher', async () => {
      const { html } = await setup({
        summary: unitsSummary({
          active: 40,
          deactivated: 2,
          apartments: 38,
          houses: 2,
          idealFractionTotal: 0.875,
          withoutIdealFraction: 5,
          withoutArea: 12,
        }),
      });

      expect(text(html, 'summary-active')).toContain('40');
      expect(text(html, 'summary-active')).toContain('Ativas');
      expect(text(html, 'summary-deactivated')).toContain('2');
      expect(text(html, 'summary-deactivated')).toContain('Desativadas');
      expect(text(html, 'summary-apartments')).toContain('38');
      expect(text(html, 'summary-houses')).toContain('2');
      expect(text(html, 'summary-fraction')).toContain('87,5% de 100%');
      expect(text(html, 'summary-without-fraction')).toContain('5');
      expect(text(html, 'summary-without-area')).toContain('12');
    });
  });

  describe('lista', () => {
    it('mostra uma linha por unidade, com bloco e numero, tipo, fracao, area e situacao', async () => {
      const { html } = await setup();

      const [first, second] = rows(html);
      expect(rows(html)).toHaveLength(2);
      expect(rowText(first)).toContain('Bloco A · 101');
      expect(rowText(first)).toContain('Apartamento');
      expect(rowText(first)).toContain('1,25%');
      expect(rowText(first)).toContain('52,5 m²');
      expect(rowText(first)).toContain('Ativa');
      expect(rowText(second)).toContain('Casa');
      expect(rowText(second)).toContain('—');
    });

    it('descreve a tabela para leitores de tela', async () => {
      const { html } = await setup();

      expect(html.querySelector('table caption')?.textContent?.trim()).toBe('Lista de unidades');
      const headers = [...html.querySelectorAll('th')].map((header) => header.textContent!.trim());
      expect(headers).toEqual(['Unidade', 'Tipo', 'Fração ideal', 'Área', 'Situação', 'Ações']);
      expect(html.querySelectorAll('th[scope="col"]')).toHaveLength(6);
    });

    it('unidade desativada so pode ser reativada e mostra a situacao em texto', async () => {
      const { html } = await setup({ items: [OFF] });

      const [row] = rows(html);
      expect(rowText(row)).toContain('Desativada');
      expect(row.querySelector('mat-icon')).not.toBeNull();
      expect(button(row, 'Reativar Bloco B · 201')).not.toBeNull();
      expect(button(row, 'Editar Bloco B · 201')).toBeNull();
      expect(button(row, 'Desativar Bloco B · 201')).toBeNull();
    });

    it('unidade ativa pode ser editada e desativada', async () => {
      const { html } = await setup();

      const [row] = rows(html);
      expect(button(row, 'Editar Bloco A · 101')).not.toBeNull();
      expect(button(row, 'Desativar Bloco A · 101')).not.toBeNull();
      expect(button(row, 'Reativar Bloco A · 101')).toBeNull();
    });

    it('conta as unidades no plural e no singular', async () => {
      const many = await setup({ items: [FIRST], total: 120 });
      expect(text(many.html, 'total')).toBe('120 unidades');
      TestBed.resetTestingModule();

      const one = await setup({ items: [FIRST], total: 1 });
      expect(text(one.html, 'total')).toBe('1 unidade');
    });

    it('sem nenhuma unidade, convida a cadastrar', async () => {
      const { html } = await setup({ items: [], total: 0 });

      expect(text(html, 'empty')).toContain('Nenhuma unidade cadastrada ainda');
      expect(text(html, 'empty')).toContain('cadastro em lote');
      expect(testId(html, 'units-table')).toBeNull();
      expect(testId(html, 'no-results')).toBeNull();
    });
  });

  describe('filtros', () => {
    it('oferece situacao, tipo, bloco e busca', async () => {
      const { html } = await setup();

      const status = testId(html, 'filter-status') as HTMLSelectElement;
      expect([...status.options].map((option) => option.textContent!.trim())).toEqual([
        'Ativas',
        'Desativadas',
        'Todas',
      ]);
      expect(status.value).toBe('active');
      const type = testId(html, 'filter-type') as HTMLSelectElement;
      expect([...type.options].map((option) => option.textContent!.trim())).toEqual([
        'Todos os tipos',
        'Apartamentos',
        'Casas',
      ]);
      expect(testId(html, 'filter-block')).not.toBeNull();
      expect(testId(html, 'filter-search')).not.toBeNull();
    });

    it.each([
      ['filter-status', 'deactivated', 'status', 'deactivated'],
      ['filter-status', 'all', 'status', 'all'],
      ['filter-type', 'house', 'type', 'house'],
      ['filter-block', 'B', 'block', 'B'],
      ['filter-search', '10', 'search', '10'],
    ])('mexer em %s para "%s" busca de novo a partir da pagina 1', async (id, value, key, sent) => {
      const { http, html, fixture } = await setup({ items: [FIRST], total: 120 });

      choose(html, id, value);
      const request = listRequest(http);
      request.flush(unitPage([FIRST], 1));
      await settle(fixture);

      expect(params(request).get(key)).toBe(sent);
      expect(params(request).get('page')).toBe('1');
      http.expectNone('/api/units/summary');
    });

    it('sem resultado com filtro, avisa e oferece limpar os filtros', async () => {
      const { http, html, fixture } = await setup();

      choose(html, 'filter-search', 'zzz');
      listRequest(http).flush(unitPage([], 0));
      await settle(fixture);

      expect(text(html, 'no-results')).toContain('Nenhuma unidade encontrada com estes filtros');
      expect(testId(html, 'empty')).toBeNull();

      testId(html, 'clear-filters')!.click();
      const request = listRequest(http);
      request.flush(unitPage([FIRST, SECOND]));
      await settle(fixture);

      expect(params(request).toString()).toBe('status=active&page=1&pageSize=50');
      expect((testId(html, 'filter-search') as HTMLInputElement).value).toBe('');
      expect(testId(html, 'clear-filters')).toBeNull();
    });
  });

  describe('paginacao', () => {
    it('some quando cabe tudo numa pagina', async () => {
      const { html } = await setup();

      expect(testId(html, 'pagination')).toBeNull();
    });

    it('mostra a pagina atual e navega para a proxima e para a anterior', async () => {
      const { http, html, fixture } = await setup({ items: [FIRST], total: 120 });

      expect(text(html, 'page-status')).toBe('Página 1 de 3');
      expect((testId(html, 'previous') as HTMLButtonElement).disabled).toBe(true);

      testId(html, 'next')!.click();
      const next = listRequest(http);
      next.flush(unitPage([SECOND], 120, 2));
      await settle(fixture);
      expect(params(next).get('page')).toBe('2');
      expect(text(html, 'page-status')).toBe('Página 2 de 3');

      testId(html, 'previous')!.click();
      const previous = listRequest(http);
      previous.flush(unitPage([FIRST], 120, 1));
      await settle(fixture);
      expect(params(previous).get('page')).toBe('1');
    });

    it('desabilita "Próxima" na ultima pagina', async () => {
      const { http, html, fixture } = await setup({ items: [FIRST], total: 120 });

      for (const page of [2, 3]) {
        testId(html, 'next')!.click();
        listRequest(http).flush(unitPage([FIRST], 120, page));
        await settle(fixture);
      }

      expect((testId(html, 'next') as HTMLButtonElement).disabled).toBe(true);
    });
  });

  describe('nova unidade', () => {
    it('abre o dialogo, grava e atualiza a lista e o resumo', async () => {
      const { http, html, fixture } = await setup();

      testId(html, 'new-unit')!.click();
      TestBed.tick();
      expect(dialog()?.querySelector('h2')?.textContent?.trim()).toBe('Nova unidade');
      typeInDialog('number', '301');
      submitDialog();
      await settle(fixture);

      const post = http.expectOne(
        (request) => request.method === 'POST' && request.url === '/api/units',
      );
      expect(post.request.body).toEqual({
        block: null,
        number: '301',
        type: 'apartment',
        idealFraction: null,
        area: null,
      });
      post.flush(unit());
      await settle(fixture);
      answerRefresh(http, [FIRST, SECOND, unit({ number: '301' })]);
      await settle(fixture);

      expect(dialog()).toBeNull();
      expect(rows(html)).toHaveLength(3);
    });

    it('explica no dialogo por que a API recusou, sem fechar', async () => {
      const { http, html, fixture } = await setup();
      testId(html, 'new-unit')!.click();
      TestBed.tick();
      typeInDialog('number', '101');
      typeInDialog('block', 'A');

      submitDialog();
      await settle(fixture);
      const [body, options] = failure(409, 'unit.identifier.already_exists');
      http.expectOne('/api/units').flush(body, options);
      await settle(fixture);

      expect(dialogAlert()).toBe('Já existe uma unidade com este bloco e número');
      expect(dialog()).not.toBeNull();
    });

    it.each([
      [
        422,
        'unit.ideal_fraction.sum_exceeded',
        'A soma das frações ideais das unidades ativas não pode passar de 100%',
      ],
      [
        400,
        'unit.area.invalid',
        'Informe uma área maior que 0 e até 99.999,99 m², com até 2 casas decimais',
      ],
      [
        429,
        'rate_limit.exceeded',
        'Muitas tentativas em pouco tempo. Aguarde alguns instantes e tente de novo',
      ],
      [500, 'internal', 'Algo deu errado. Tente novamente em instantes'],
    ])('traduz %s %s no dialogo', async (status, code, message) => {
      const { http, html, fixture } = await setup();
      testId(html, 'new-unit')!.click();
      TestBed.tick();
      typeInDialog('number', '301');

      submitDialog();
      await settle(fixture);
      const [body, options] = failure(status, code);
      http.expectOne('/api/units').flush(body, options);
      await settle(fixture);

      expect(dialogAlert()).toBe(message);
    });
  });

  describe('editar unidade', () => {
    it('abre com os dados da unidade e grava com a versao lida', async () => {
      const { http, html, fixture } = await setup();

      button(rows(html)[0], 'Editar Bloco A · 101').click();
      TestBed.tick();
      expect(dialog()?.querySelector('h2')?.textContent?.trim()).toBe('Editar unidade');
      typeInDialog('area', '60');
      submitDialog();
      await settle(fixture);

      const put = http.expectOne(`/api/units/${FIRST.id}`);
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual({
        block: 'A',
        number: '101',
        type: 'apartment',
        idealFraction: 0.0125,
        area: 60,
        version: '41',
      });
      put.flush({ ...FIRST, area: 60, version: '42' });
      await settle(fixture);
      answerRefresh(http, [{ ...FIRST, area: 60, version: '42' }, SECOND]);
      await settle(fixture);

      expect(dialog()).toBeNull();
      expect(rowText(rows(html)[0])).toContain('60 m²');
    });

    it('quando outra pessoa mexeu antes, avisa no dialogo e atualiza a lista por tras', async () => {
      const { http, html, fixture } = await setup();
      button(rows(html)[0], 'Editar Bloco A · 101').click();
      TestBed.tick();
      typeInDialog('area', '60');

      submitDialog();
      await settle(fixture);
      const [body, options] = failure(409, 'concurrency.conflict');
      http.expectOne(`/api/units/${FIRST.id}`).flush(body, options);
      await settle(fixture);
      answerRefresh(http, [{ ...FIRST, area: 70, version: '43' }, SECOND]);
      await settle(fixture);

      expect(dialogAlert()).toBe(
        'Esta unidade foi alterada em outro lugar. Feche esta janela e confira a lista, que já foi atualizada',
      );
      expect(rowText(rows(html)[0])).toContain('70 m²');
    });
  });

  describe('desativar e reativar', () => {
    it('desativa sem pedir confirmacao, com a versao lida, e avisa', async () => {
      const { http, html, fixture } = await setup();

      button(rows(html)[0], 'Desativar Bloco A · 101').click();
      await settle(fixture);
      const post = http.expectOne(`/api/units/${FIRST.id}/deactivate`);
      expect(post.request.body).toEqual({ version: '41' });
      post.flush({ ...FIRST, isActive: false });
      await settle(fixture);
      answerRefresh(http, [SECOND]);
      await settle(fixture);

      expect(rows(html)).toHaveLength(1);
      expect(testId(html, 'notice')?.getAttribute('role')).toBe('status');
      expect(text(html, 'notice')).toBe('Bloco A · 101 desativada');
      expect(dialog()).toBeNull();
    });

    it('reativa com a versao lida e avisa', async () => {
      const { http, html, fixture } = await setup({ items: [OFF] });

      button(rows(html)[0], 'Reativar Bloco B · 201').click();
      await settle(fixture);
      const post = http.expectOne(`/api/units/${OFF.id}/reactivate`);
      expect(post.request.body).toEqual({ version: OFF.version });
      post.flush({ ...OFF, isActive: true });
      await settle(fixture);
      answerRefresh(http, []);
      await settle(fixture);

      expect(text(html, 'notice')).toBe('Bloco B · 201 reativada');
    });

    it.each([
      [
        409,
        'concurrency.conflict',
        'Esta unidade foi alterada em outro lugar. A lista já foi atualizada',
      ],
      [404, 'unit.not_found', 'Esta unidade não existe mais. A lista já foi atualizada'],
      [
        409,
        'unit.already_deactivated',
        'Esta unidade já estava desativada. A lista já foi atualizada',
      ],
    ])('com %s %s, explica e atualiza a lista', async (status, code, message) => {
      const { http, html, fixture } = await setup();

      button(rows(html)[0], 'Desativar Bloco A · 101').click();
      await settle(fixture);
      const [body, options] = failure(status, code);
      http.expectOne(`/api/units/${FIRST.id}/deactivate`).flush(body, options);
      await settle(fixture);
      answerRefresh(http, [SECOND]);
      await settle(fixture);

      expect(text(html, 'action-error')).toBe(message);
      expect(testId(html, 'action-error')?.getAttribute('role')).toBe('alert');
      expect(rows(html)).toHaveLength(1);
      expect(testId(html, 'notice')).toBeNull();
    });

    it('ao reativar, avisa quando a soma das fracoes passaria de 100%', async () => {
      const { http, html, fixture } = await setup({ items: [OFF] });

      button(rows(html)[0], 'Reativar Bloco B · 201').click();
      await settle(fixture);
      const [body, options] = failure(422, 'unit.ideal_fraction.sum_exceeded');
      http.expectOne(`/api/units/${OFF.id}/reactivate`).flush(body, options);
      await settle(fixture);

      expect(text(html, 'action-error')).toBe(
        'A soma das frações ideais das unidades ativas não pode passar de 100%',
      );
    });

    it('a mensagem de erro some na proxima acao bem-sucedida', async () => {
      const { http, html, fixture } = await setup();
      button(rows(html)[0], 'Desativar Bloco A · 101').click();
      await settle(fixture);
      const [body, options] = failure(429, 'rate_limit.exceeded');
      http.expectOne(`/api/units/${FIRST.id}/deactivate`).flush(body, options);
      await settle(fixture);
      expect(testId(html, 'action-error')).not.toBeNull();

      button(rows(html)[0], 'Desativar Bloco A · 101').click();
      await settle(fixture);
      http.expectOne(`/api/units/${FIRST.id}/deactivate`).flush({ ...FIRST, isActive: false });
      await settle(fixture);
      answerRefresh(http, [SECOND]);
      await settle(fixture);

      expect(testId(html, 'action-error')).toBeNull();
    });

    it('trava os botoes de acao enquanto uma chamada esta em andamento', async () => {
      const { http, html, fixture } = await setup();

      button(rows(html)[0], 'Desativar Bloco A · 101').click();
      await settle(fixture);

      expect(button(rows(html)[1], 'Desativar Bloco A · 102').disabled).toBe(true);
      expect((testId(html, 'new-unit') as HTMLButtonElement).disabled).toBe(true);
      http.expectOne(`/api/units/${FIRST.id}/deactivate`).flush({ ...FIRST, isActive: false });
      await settle(fixture);
      answerRefresh(http, [SECOND]);
      await settle(fixture);
      expect(button(rows(html)[0], 'Desativar Bloco A · 102').disabled).toBe(false);
    });
  });

  describe('metodo de rateio', () => {
    it('mostra o cartao com o metodo escolhido', async () => {
      const { html } = await setup();

      expect(html.querySelector('urban-apportionment-card')).not.toBeNull();
      expect(
        html.querySelector<HTMLInputElement>('input[type="radio"][value="idealFraction"]')!.checked,
      ).toBe(true);
    });

    it('troca o metodo com a versao do resumo e passa a mostrar o novo', async () => {
      const { http, html, fixture } = await setup();

      html.querySelector<HTMLInputElement>('input[type="radio"][value="area"]')!.click();
      await settle(fixture);
      const put = http.expectOne('/api/units/apportionment-method');
      expect(put.request.body).toEqual({ version: '12', method: 'area' });
      put.flush({ method: 'area', version: '13' });
      await settle(fixture);

      expect(
        html.querySelector<HTMLInputElement>('input[type="radio"][value="area"]')!.checked,
      ).toBe(true);
      expect(testId(html, 'method-error')).toBeNull();
    });

    it('quando outra pessoa trocou antes, avisa e mostra o metodo atual', async () => {
      const { http, html, fixture } = await setup();

      html.querySelector<HTMLInputElement>('input[type="radio"][value="area"]')!.click();
      await settle(fixture);
      const [body, options] = failure(409, 'concurrency.conflict');
      http.expectOne('/api/units/apportionment-method').flush(body, options);
      await settle(fixture);
      http
        .expectOne('/api/units/summary')
        .flush(unitsSummary({ apportionmentMethod: 'equalShare', apportionmentVersion: '14' }));
      await settle(fixture);

      expect(text(html, 'method-error')).toBe(
        'O método de rateio foi alterado em outro lugar. Confira a escolha atual e tente de novo',
      );
      expect(testId(html, 'method-error')?.getAttribute('role')).toBe('alert');
      expect(
        html.querySelector<HTMLInputElement>('input[type="radio"][value="equalShare"]')!.checked,
      ).toBe(true);
    });

    it('explica outras falhas da troca', async () => {
      const { http, html, fixture } = await setup();

      html.querySelector<HTMLInputElement>('input[type="radio"][value="area"]')!.click();
      await settle(fixture);
      http
        .expectOne('/api/units/apportionment-method')
        .flush(null, { status: 503, statusText: 'Erro' });
      await settle(fixture);

      expect(text(html, 'method-error')).toBe(
        'Serviço temporariamente indisponível. Tente novamente em instantes',
      );
    });
  });
});
