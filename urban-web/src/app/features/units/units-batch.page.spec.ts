import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { provideRouter } from '@angular/router';
import { jest } from '@jest/globals';
import { SessionStore } from '../../core/auth/session.store';
import { provideUrbanIcons } from '../../core/theme/icons';
import { fieldValueOf, setFieldValue } from '../../testing/menu-select';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, signInAs } from '../../testing/auth.fixtures';
import { unitsSummary } from '../../testing/units.fixtures';
import { UnitsBatchPage } from './units-batch.page';

interface Harness {
  fixture: ComponentFixture<UnitsBatchPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

async function setup(summary: 'ok' | 'fail' = 'ok'): Promise<Harness> {
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
  const fixture = TestBed.createComponent(UnitsBatchPage);
  fixture.detectChanges();
  const request = http.expectOne('/api/units/summary');
  if (summary === 'ok') {
    request.flush(unitsSummary({ idealFractionTotal: 0.5 }));
  } else {
    request.flush(null, { status: 503, statusText: 'Erro' });
  }
  await settle(fixture);
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

function text(html: HTMLElement, id: string): string {
  return testId(html, id)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

function generator(html: HTMLElement): HTMLElement {
  return testId(html, 'generator')!;
}

function typeInto(element: HTMLElement, value: string): void {
  setFieldValue(element, value);
}

function fillGenerator(html: HTMLElement, name: string, value: string): void {
  typeInto(generator(html).querySelector<HTMLInputElement>(`[formControlName="${name}"]`)!, value);
}

function chooseKind(html: HTMLElement, kind: 'apartment' | 'house'): void {
  generator(html).querySelector<HTMLInputElement>(`input[type="radio"][value="${kind}"]`)!.click();
}

function rows(html: HTMLElement): HTMLElement[] {
  return [...html.querySelectorAll<HTMLElement>('[data-testid="batch-row"]')];
}

function cell(row: HTMLElement, field: string): HTMLInputElement {
  return row.querySelector<HTMLInputElement>(`[data-field="${field}"]`)!;
}

function numbers(html: HTMLElement): string[] {
  return rows(html).map((row) => cell(row, 'number').value);
}

function rowErrors(row: HTMLElement): string {
  return (
    row.querySelector('[data-testid="row-error"]')?.textContent?.replace(/\s+/g, ' ').trim() ?? ''
  );
}

async function buildFloors(
  h: Harness,
  first: string,
  last: string,
  perFloor: string,
): Promise<void> {
  fillGenerator(h.html, 'firstFloor', first);
  fillGenerator(h.html, 'lastFloor', last);
  fillGenerator(h.html, 'perFloor', perFloor);
  testId(h.html, 'build-floors')!.click();
  await settle(h.fixture);
}

async function generate(h: Harness): Promise<void> {
  testId(h.html, 'generate')!.click();
  await settle(h.fixture);
}

async function generateApartments(
  h: Harness,
  first: string,
  last: string,
  perFloor: string,
): Promise<void> {
  await buildFloors(h, first, last, perFloor);
  await generate(h);
}

async function generateHouses(h: Harness, count: string): Promise<void> {
  chooseKind(h.html, 'house');
  await settle(h.fixture);
  fillGenerator(h.html, 'houseCount', count);
  await generate(h);
}

function failure(status: number, body: object): [unknown, { status: number; statusText: string }] {
  return [
    { status, ...body },
    { status, statusText: 'Erro' },
  ];
}

async function submit(h: Harness): Promise<void> {
  testId(h.html, 'submit')!.click();
  await settle(h.fixture);
}

describe('UnitsBatchPage', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    TestBed.inject(HttpTestingController).match(() => true);
  });

  describe('ao abrir', () => {
    it('explica a tela, volta para a lista e busca o resumo', async () => {
      const { html } = await setup();

      expect(html.querySelector('h1')?.textContent?.trim()).toBe('Cadastro em lote');
      const back = testId(html, 'back') as HTMLAnchorElement;
      expect(back.getAttribute('href')).toBe('/unidades');
      expect(back.textContent).toContain('Voltar para as unidades');
      expect(html.textContent).toContain('até 500 unidades por vez');
      expect(html.textContent).toContain(
        'Se alguma linha tiver problema, nenhuma unidade é cadastrada',
      );
    });

    it('comeca em apartamentos, sem linhas e com o cadastro desabilitado', async () => {
      const { html } = await setup();

      expect(
        generator(html).querySelector<HTMLInputElement>('input[value="apartment"]')!.checked,
      ).toBe(true);
      expect(rows(html)).toHaveLength(0);
      expect(testId(html, 'preview')).toBeNull();
      expect((testId(html, 'submit') as HTMLButtonElement).disabled).toBe(true);
    });
  });

  describe('gerador por andar', () => {
    it('monta uma linha por andar, todos com a mesma quantidade', async () => {
      const h = await setup();

      await buildFloors(h, '1', '3', '2');

      const counts = [...h.html.querySelectorAll<HTMLInputElement>('[data-testid="floor-count"]')];
      expect(counts.map((input) => input.dataset['floor'])).toEqual(['1', '2', '3']);
      expect(counts.map((input) => input.value)).toEqual(['2', '2', '2']);
      expect(text(h.html, 'floor-plan')).toContain('Andar 1');
      expect(text(h.html, 'floor-plan')).toContain('6 unidades');
    });

    it('chama o andar 0 de terreo', async () => {
      const h = await setup();

      await buildFloors(h, '0', '1', '2');

      expect(text(h.html, 'floor-plan')).toContain('Térreo');
    });

    it('gera os apartamentos com numeros por andar, aceitando quantidades diferentes', async () => {
      const h = await setup();
      await buildFloors(h, '1', '3', '2');
      const counts = h.html.querySelectorAll<HTMLInputElement>('[data-testid="floor-count"]');
      typeInto(counts[1], '3');
      typeInto(counts[2], '0');
      await settle(h.fixture);

      await generate(h);

      expect(numbers(h.html)).toEqual(['101', '102', '201', '202', '203']);
      expect(text(h.html, 'row-count')).toBe('5 unidades no lote');
    });

    it('usa o bloco informado e deixa tipo, fracao e area prontos para editar', async () => {
      const h = await setup();
      fillGenerator(h.html, 'block', 'A');

      await generateApartments(h, '1', '1', '1');

      const [row] = rows(h.html);
      expect(cell(row, 'block').value).toBe('A');
      expect(fieldValueOf(cell(row, 'type'))).toBe('apartment');
      expect(cell(row, 'fraction').value).toBe('');
      expect(cell(row, 'area').value).toBe('');
    });

    it('gera casas em sequencia', async () => {
      const h = await setup();

      await generateHouses(h, '3');

      expect(numbers(h.html)).toEqual(['1', '2', '3']);
      expect(fieldValueOf(cell(rows(h.html)[0], 'type'))).toBe('house');
    });

    it('so mostra os campos do tipo escolhido', async () => {
      const h = await setup();
      expect(generator(h.html).querySelector('[formControlName="firstFloor"]')).not.toBeNull();
      expect(generator(h.html).querySelector('[formControlName="houseCount"]')).toBeNull();

      chooseKind(h.html, 'house');
      await settle(h.fixture);

      expect(generator(h.html).querySelector('[formControlName="firstFloor"]')).toBeNull();
      expect(generator(h.html).querySelector('[formControlName="houseCount"]')).not.toBeNull();
    });

    it('acrescenta ao que ja esta no lote, para montar um bloco depois do outro', async () => {
      const h = await setup();
      fillGenerator(h.html, 'block', 'A');
      await generateApartments(h, '1', '1', '2');
      fillGenerator(h.html, 'block', 'B');

      await generateApartments(h, '1', '1', '2');

      expect(rows(h.html).map((row) => cell(row, 'block').value)).toEqual(['A', 'A', 'B', 'B']);
    });

    it('depois de gerar, esconde a lista de andares para comecar outra', async () => {
      const h = await setup();

      await generateApartments(h, '1', '2', '2');

      expect(testId(h.html, 'floor-plan')).toBeNull();
    });

    it.each([
      [{ first: '', last: '3', per: '2' }, 'Informe o primeiro andar, de 0 a 99'],
      [{ first: '1', last: '', per: '2' }, 'Informe o último andar, de 0 a 99'],
      [{ first: '1', last: '100', per: '2' }, 'Informe o último andar, de 0 a 99'],
      [{ first: '5', last: '2', per: '2' }, 'O último andar não pode ser menor que o primeiro'],
      [{ first: '1', last: '3', per: '0' }, 'Informe de 1 a 99 unidades por andar'],
      [{ first: '1', last: '3', per: 'x' }, 'Informe de 1 a 99 unidades por andar'],
    ])('nao monta os andares com %j e explica o motivo', async ({ first, last, per }, message) => {
      const h = await setup();

      await buildFloors(h, first, last, per);

      expect(testId(h.html, 'floor-plan')).toBeNull();
      expect(generator(h.html).textContent).toContain(message);
    });

    it('recusa passar de 500 unidades e nao acrescenta nada', async () => {
      const h = await setup();

      await generateApartments(h, '1', '99', '6');

      expect(rows(h.html)).toHaveLength(0);
      expect(text(h.html, 'generator-error')).toBe('O lote aceita até 500 unidades por vez');
    });

    it('conta o que ja esta no lote para respeitar o limite', async () => {
      const h = await setup();
      await generateApartments(h, '1', '50', '9');
      expect(rows(h.html)).toHaveLength(450);

      await generateApartments(h, '1', '10', '6');

      expect(rows(h.html)).toHaveLength(450);
      expect(text(h.html, 'generator-error')).toBe('O lote aceita até 500 unidades por vez');
    });

    it('recusa gerar casas com quantidade invalida', async () => {
      const h = await setup();

      await generateHouses(h, '0');

      expect(rows(h.html)).toHaveLength(0);
      expect(generator(h.html).textContent).toContain('Informe de 1 a 500 casas');
    });
  });

  describe('pre-visualizacao', () => {
    it('deixa corrigir o numero, o bloco, o tipo, a fracao e a area de cada linha', async () => {
      const h = await setup();
      await generateApartments(h, '1', '1', '2');
      const [first] = rows(h.html);

      typeInto(cell(first, 'number'), '1A');
      typeInto(cell(first, 'block'), 'Torre');
      typeInto(cell(first, 'type'), 'house');
      typeInto(cell(first, 'fraction'), '1,5');
      typeInto(cell(first, 'area'), '60');
      await settle(h.fixture);

      const edited = rows(h.html)[0];
      expect(cell(edited, 'number').value).toBe('1A');
      expect(cell(edited, 'block').value).toBe('Torre');
      expect(fieldValueOf(cell(edited, 'type'))).toBe('house');
      expect(cell(edited, 'fraction').value).toBe('1,5');
      expect(cell(edited, 'area').value).toBe('60');
      expect(rowErrors(edited)).toBe('');
    });

    it('marca a linha sem numero, com numero ou bloco longos, ou com fracao e area invalidas', async () => {
      const h = await setup();
      await generateApartments(h, '1', '1', '2');
      const [first, second] = rows(h.html);

      typeInto(cell(first, 'number'), '');
      typeInto(cell(second, 'fraction'), '150');
      typeInto(cell(second, 'area'), 'x');
      await settle(h.fixture);

      expect(rowErrors(rows(h.html)[0])).toContain('Informe o número da unidade');
      expect(cell(rows(h.html)[0], 'number').getAttribute('aria-invalid')).toBe('true');
      expect(rowErrors(rows(h.html)[1])).toContain('Informe uma fração maior que 0% e até 100%');
      expect(rowErrors(rows(h.html)[1])).toContain('Informe uma área maior que 0');
    });

    it('marca as linhas que repetem bloco e numero', async () => {
      const h = await setup();
      await generateApartments(h, '1', '1', '3');
      typeInto(cell(rows(h.html)[2], 'number'), '101');
      await settle(h.fixture);

      expect(rowErrors(rows(h.html)[0])).toBe(
        'Este bloco e número aparecem mais de uma vez no lote',
      );
      expect(rowErrors(rows(h.html)[1])).toBe('');
      expect(rowErrors(rows(h.html)[2])).toBe(
        'Este bloco e número aparecem mais de uma vez no lote',
      );
    });

    it('remove uma linha', async () => {
      const h = await setup();
      await generateApartments(h, '1', '1', '3');

      rows(h.html)[1].querySelector<HTMLButtonElement>('[data-action="remove"]')!.click();
      await settle(h.fixture);

      expect(numbers(h.html)).toEqual(['101', '103']);
      expect(text(h.html, 'row-count')).toBe('2 unidades no lote');
    });

    it('acrescenta uma linha em branco com o bloco da ultima', async () => {
      const h = await setup();
      fillGenerator(h.html, 'block', 'A');
      await generateApartments(h, '1', '1', '1');

      testId(h.html, 'add-row')!.click();
      await settle(h.fixture);

      const added = rows(h.html)[1];
      expect(cell(added, 'number').value).toBe('');
      expect(cell(added, 'block').value).toBe('A');
      expect(fieldValueOf(cell(added, 'type'))).toBe('apartment');
    });

    it('permite comecar so com linhas manuais', async () => {
      const h = await setup();

      testId(h.html, 'add-row')!.click();
      await settle(h.fixture);

      expect(rows(h.html)).toHaveLength(1);
    });

    it('limpa o lote todo', async () => {
      const h = await setup();
      await generateApartments(h, '1', '1', '3');

      testId(h.html, 'clear-all')!.click();
      await settle(h.fixture);

      expect(rows(h.html)).toHaveLength(0);
      expect(testId(h.html, 'preview')).toBeNull();
    });

    it('conta uma unidade no singular', async () => {
      const h = await setup();
      await generateApartments(h, '1', '1', '1');

      expect(text(h.html, 'row-count')).toBe('1 unidade no lote');
    });
  });

  describe('dividir a fracao', () => {
    it('reparte o que falta para 100% entre as linhas sem fracao', async () => {
      const h = await setup();
      await generateApartments(h, '1', '1', '2');

      testId(h.html, 'split-fraction')!.click();
      await settle(h.fixture);

      expect(rows(h.html).map((row) => cell(row, 'fraction').value)).toEqual(['25', '25']);
      expect(testId(h.html, 'split-hint')?.textContent).toContain('50% já estão ocupados');
    });

    it('fica desabilitado sem linhas sem fracao ou sem o resumo', async () => {
      const h = await setup();
      expect((testId(h.html, 'split-fraction') as HTMLButtonElement).disabled).toBe(true);
      TestBed.resetTestingModule();

      const failed = await setup('fail');
      await generateApartments(failed, '1', '1', '2');

      expect((testId(failed.html, 'split-fraction') as HTMLButtonElement).disabled).toBe(true);
    });
  });

  describe('cadastrar', () => {
    it('envia as linhas na ordem da tela, com a fracao em proporcao', async () => {
      const h = await setup();
      fillGenerator(h.html, 'block', 'A');
      await generateApartments(h, '1', '1', '2');
      typeInto(cell(rows(h.html)[0], 'fraction'), '1,25');
      typeInto(cell(rows(h.html)[0], 'area'), '52,5');
      await settle(h.fixture);

      expect(text(h.html, 'submit')).toBe('Cadastrar 2 unidades');
      await submit(h);

      const post = h.http.expectOne('/api/units/batch');
      expect(post.request.body).toEqual({
        units: [
          { block: 'A', number: '101', type: 'apartment', idealFraction: 0.0125, area: 52.5 },
          { block: 'A', number: '102', type: 'apartment', idealFraction: null, area: null },
        ],
      });
      post.flush({ created: 2 });
      await settle(h.fixture);

      expect(text(h.html, 'batch-done')).toContain('2 unidades cadastradas');
      expect(testId(h.html, 'batch-done')?.getAttribute('role')).toBe('status');
      expect((testId(h.html, 'to-units') as HTMLAnchorElement).getAttribute('href')).toBe(
        '/unidades',
      );
      expect(testId(h.html, 'preview')).toBeNull();
      expect(testId(h.html, 'generator')).toBeNull();
    });

    it('conta uma unidade cadastrada no singular', async () => {
      const h = await setup();
      await generateApartments(h, '1', '1', '1');

      await submit(h);
      h.http.expectOne('/api/units/batch').flush({ created: 1 });
      await settle(h.fixture);

      expect(text(h.html, 'batch-done')).toContain('1 unidade cadastrada');
    });

    it('"Cadastrar mais" volta ao gerador, com o lote vazio', async () => {
      const h = await setup();
      await generateApartments(h, '1', '1', '1');
      await submit(h);
      h.http.expectOne('/api/units/batch').flush({ created: 1 });
      await settle(h.fixture);

      testId(h.html, 'start-over')!.click();
      await settle(h.fixture);

      expect(testId(h.html, 'batch-done')).toBeNull();
      expect(testId(h.html, 'generator')).not.toBeNull();
      expect(rows(h.html)).toHaveLength(0);
    });

    it('nao deixa cadastrar com linha invalida ou repetida, e diz o motivo', async () => {
      const h = await setup();
      await generateApartments(h, '1', '1', '2');
      typeInto(cell(rows(h.html)[0], 'number'), '');
      await settle(h.fixture);

      expect((testId(h.html, 'submit') as HTMLButtonElement).disabled).toBe(true);
      expect(text(h.html, 'fix-rows')).toBe('Corrija as linhas marcadas para cadastrar');
    });

    it('trava o botao e troca o texto enquanto cadastra', async () => {
      const h = await setup();
      await generateApartments(h, '1', '1', '2');

      await submit(h);

      const button = testId(h.html, 'submit') as HTMLButtonElement;
      expect(button.disabled).toBe(true);
      expect(button.textContent?.trim()).toBe('Cadastrando');
      h.http.expectOne('/api/units/batch').flush({ created: 2 });
      await settle(h.fixture);
    });

    describe('quando a API recusa', () => {
      async function rejected(h: Harness, status: number, body: object): Promise<void> {
        await submit(h);
        const [payload, options] = failure(status, body);
        h.http.expectOne('/api/units/batch').flush(payload, options);
        await settle(h.fixture);
      }

      it('marca cada linha com o motivo e explica que nada foi cadastrado', async () => {
        const h = await setup();
        await generateApartments(h, '1', '1', '3');

        await rejected(h, 409, {
          code: 'unit.identifier.already_exists',
          errors: [
            { position: 0, code: 'unit.identifier.already_exists' },
            { position: 2, code: 'unit.number.invalid' },
          ],
        });

        expect(rowErrors(rows(h.html)[0])).toBe('Já existe uma unidade com este bloco e número');
        expect(rowErrors(rows(h.html)[1])).toBe('');
        expect(rowErrors(rows(h.html)[2])).toBe(
          'Informe o número da unidade, com até 20 caracteres',
        );
        expect(text(h.html, 'batch-error')).toBe(
          'Nenhuma unidade foi cadastrada. Corrija as linhas marcadas e tente de novo',
        );
        expect(testId(h.html, 'batch-error')?.getAttribute('role')).toBe('alert');
        expect(rows(h.html)).toHaveLength(3);
      });

      it('tira a marca da linha que a pessoa corrige, sem mexer nas outras', async () => {
        const h = await setup();
        await generateApartments(h, '1', '1', '3');
        await rejected(h, 409, {
          errors: [
            { position: 0, code: 'unit.identifier.already_exists' },
            { position: 1, code: 'unit.identifier.already_exists' },
          ],
        });

        typeInto(cell(rows(h.html)[0], 'number'), '150');
        await settle(h.fixture);

        expect(rowErrors(rows(h.html)[0])).toBe('');
        expect(rowErrors(rows(h.html)[1])).toBe('Já existe uma unidade com este bloco e número');
      });

      it('apaga todas as marcas se uma linha e removida, porque as posicoes mudam', async () => {
        const h = await setup();
        await generateApartments(h, '1', '1', '3');
        await rejected(h, 409, {
          errors: [{ position: 2, code: 'unit.identifier.already_exists' }],
        });

        rows(h.html)[0].querySelector<HTMLButtonElement>('[data-action="remove"]')!.click();
        await settle(h.fixture);

        expect(rows(h.html).every((row) => rowErrors(row) === '')).toBe(true);
        expect(testId(h.html, 'batch-error')).toBeNull();
      });

      it('explica a soma das fracoes acima de 100%', async () => {
        const h = await setup();
        await generateApartments(h, '1', '1', '2');

        await rejected(h, 422, {
          code: 'unit.ideal_fraction.sum_exceeded',
          errors: [{ position: 1, code: 'unit.ideal_fraction.sum_exceeded' }],
        });

        expect(rowErrors(rows(h.html)[1])).toBe(
          'A soma das frações ideais das unidades ativas não pode passar de 100%',
        );
      });

      it.each([
        [503, null, 'Serviço temporariamente indisponível. Tente novamente em instantes'],
        [400, { code: 'unit.batch.size_invalid' }, 'O cadastro em lote aceita de 1 a 500 unidades'],
        [
          429,
          { code: 'rate_limit.exceeded' },
          'Muitas tentativas em pouco tempo. Aguarde alguns instantes e tente de novo',
        ],
      ])(
        'com %s sem linhas marcadas, mostra so a mensagem geral',
        async (status, body, message) => {
          const h = await setup();
          await generateApartments(h, '1', '1', '2');

          await rejected(h, status, body ?? {});

          expect(text(h.html, 'batch-error')).toBe(message);
          expect(rows(h.html).every((row) => rowErrors(row) === '')).toBe(true);
        },
      );

      it('deixa tentar de novo e cadastra', async () => {
        const h = await setup();
        await generateApartments(h, '1', '1', '2');
        await rejected(h, 503, {});

        await submit(h);
        h.http.expectOne('/api/units/batch').flush({ created: 2 });
        await settle(h.fixture);

        expect(testId(h.html, 'batch-error')).toBeNull();
        expect(text(h.html, 'batch-done')).toContain('2 unidades cadastradas');
      });
    });
  });
});
