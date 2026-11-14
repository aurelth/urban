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
import { Resident } from '../../core/residents/residents.models';
import { provideUrbanIcons } from '../../core/theme/icons';
import { flushPromises } from '../../testing/async';
import { REGISTERED_MANAGER, signInAs } from '../../testing/auth.fixtures';
import { chooseMenuOption, menuSelectOptions, menuSelectValue } from '../../testing/menu-select';
import { occupancy, resident, residentPage } from '../../testing/residents.fixtures';
import { ResidentsPage } from './residents.page';

interface Harness {
  fixture: ComponentFixture<ResidentsPage>;
  http: HttpTestingController;
  html: HTMLElement;
}

const LIVES = resident({
  fullName: 'Ana Lima',
  maskedCpf: '***.123.456-**',
  phone: '47998765432',
  email: 'ana@exemplo.com',
  occupancies: [
    occupancy({ unitBlock: 'A', unitNumber: '101', kind: 'owner', startDate: '2024-03-01' }),
  ],
});
const LEFT = resident({
  fullName: 'Beto Rocha',
  phone: '4733334444',
  email: null,
  occupancies: [
    occupancy({
      unitBlock: null,
      unitNumber: '15',
      kind: 'tenant',
      startDate: '2023-01-10',
      endDate: '2026-10-31',
    }),
  ],
});

async function setup(
  items: Resident[] | 'pending' = [LIVES, LEFT],
  total?: number,
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
  await signInAs(TestBed.inject(SessionStore), http, REGISTERED_MANAGER);
  const fixture = TestBed.createComponent(ResidentsPage);
  fixture.detectChanges();
  if (items !== 'pending') {
    listRequest(http).flush(residentPage(items, total ?? items.length));
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
  return http.expectOne((request) => request.url === '/api/residents' && request.method === 'GET');
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
  return [...html.querySelectorAll<HTMLElement>('[data-testid="resident-row"]')];
}

function rowText(row: HTMLElement): string {
  return row.textContent!.replace(/\s+/g, ' ').trim();
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

describe('ResidentsPage', () => {
  afterEach(() => {
    document.querySelectorAll('.cdk-overlay-container').forEach((overlay) => overlay.remove());
    TestBed.inject(HttpTestingController).match(() => true);
  });

  describe('ao abrir', () => {
    it('mostra que está carregando enquanto a lista não chega', async () => {
      const { html } = await setup('pending');

      expect(html.querySelector('h1')?.textContent?.trim()).toBe('Moradores');
      expect(testId(html, 'loading')?.getAttribute('role')).toBe('status');
      expect(text(html, 'loading')).toBe('Carregando os moradores');
      expect(testId(html, 'residents-table')).toBeNull();
    });

    it('pede os moradores atuais da primeira página', async () => {
      const { http } = await setup('pending');

      expect(params(listRequest(http)).toString()).toBe('status=active&page=1&pageSize=50');
    });

    it('mostra o erro e deixa tentar de novo quando a busca falha', async () => {
      const { http, html, fixture } = await setup('pending');
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await settle(fixture);

      expect(text(html, 'load-error')).toContain('Não foi possível carregar os moradores');

      testId(html, 'retry')!.click();
      listRequest(http).flush(residentPage([LIVES]));
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(rows(html)).toHaveLength(1);
    });
  });

  describe('lista', () => {
    it('mostra uma linha por morador, com nome, CPF mascarado, contato, unidades e situação', async () => {
      const { html } = await setup();

      const [first, second] = rows(html);
      expect(rows(html)).toHaveLength(2);
      expect(rowText(first)).toContain('Ana Lima');
      expect(rowText(first)).toContain('***.123.456-**');
      expect(rowText(first)).toContain('(47) 99876-5432');
      expect(rowText(first)).toContain('ana@exemplo.com');
      expect(rowText(first)).toContain('Bloco A · 101');
      expect(rowText(first)).toContain('Proprietário, desde 01/03/2024');
      expect(rowText(first)).toContain('Mora');
      expect(rowText(second)).toContain('Beto Rocha');
      expect(rowText(second)).toContain('(47) 3333-4444');
      expect(rowText(second)).toContain('Sem e-mail');
    });

    it('mostra o período de quem já saiu e a situação em texto, não só em cor', async () => {
      const { html } = await setup();

      const row = rows(html)[1];
      expect(rowText(row)).toContain('Inquilino, de 10/01/2023 a 31/10/2026');
      expect(rowText(row)).toContain('Ex-morador');
      expect(row.querySelector('mat-icon')).not.toBeNull();
    });

    it('lista todos os vínculos de quem tem mais de um', async () => {
      const both = resident({
        occupancies: [
          occupancy({ unitBlock: 'A', unitNumber: '101', kind: 'owner' }),
          occupancy({ unitBlock: 'B', unitNumber: '202', kind: 'tenant', endDate: '2025-02-28' }),
        ],
      });
      const { html } = await setup([both]);

      const items = [...html.querySelectorAll('[data-testid="occupancy-item"]')];
      expect(items).toHaveLength(2);
      expect(items[0].textContent).toContain('Bloco A · 101');
      expect(items[1].textContent).toContain('Bloco B · 202');
      expect(rowText(rows(html)[0])).toContain('Mora');
      expect(rowText(rows(html)[0])).not.toContain('Ex-morador');
    });

    it('descreve a tabela para leitores de tela', async () => {
      const { html } = await setup();

      expect(html.querySelector('table caption')?.textContent?.trim()).toBe('Lista de moradores');
      const headers = [...html.querySelectorAll('th')].map((header) => header.textContent!.trim());
      expect(headers).toEqual(['Morador', 'Contato', 'Unidades', 'Situação']);
      expect(html.querySelectorAll('th[scope="col"]')).toHaveLength(4);
    });

    it('conta os moradores no plural e no singular', async () => {
      const many = await setup([LIVES], 120);
      expect(text(many.html, 'total')).toBe('120 moradores');
      TestBed.resetTestingModule();

      const one = await setup([LIVES], 1);
      expect(text(one.html, 'total')).toBe('1 morador');
    });

    it('sem nenhum morador atual, explica onde aparece quem já saiu', async () => {
      const { html } = await setup([], 0);

      expect(text(html, 'empty')).toContain('Nenhum morador atual');
      expect(text(html, 'empty')).toContain('Ex-moradores');
      expect(testId(html, 'residents-table')).toBeNull();
      expect(testId(html, 'no-results')).toBeNull();
    });
  });

  describe('filtros', () => {
    it('oferece a situação (moradores atuais por padrão) e a busca por nome', async () => {
      const { html } = await setup();

      const status = testId(html, 'filter-status')!;
      expect(menuSelectOptions(status).map((option) => option.label)).toEqual([
        'Moradores atuais',
        'Ex-moradores',
        'Todos',
      ]);
      expect(menuSelectValue(status)).toBe('active');
      expect(testId(html, 'filter-search')).not.toBeNull();
    });

    it.each([
      ['filter-status', 'former', 'status', 'former'],
      ['filter-status', 'all', 'status', 'all'],
      ['filter-search', 'maria', 'search', 'maria'],
    ])('mexer em %s para "%s" busca de novo a partir da página 1', async (id, value, key, sent) => {
      const { http, html, fixture } = await setup([LIVES], 120);

      choose(html, id, value);
      const request = listRequest(http);
      request.flush(residentPage([LIVES], 1));
      await settle(fixture);

      expect(params(request).get(key)).toBe(sent);
      expect(params(request).get('page')).toBe('1');
    });

    it('sem resultado com filtro, avisa e oferece limpar os filtros', async () => {
      const { http, html, fixture } = await setup();

      choose(html, 'filter-search', 'zzz');
      listRequest(http).flush(residentPage([], 0));
      await settle(fixture);

      expect(text(html, 'no-results')).toContain('Nenhum morador encontrado com estes filtros');
      expect(testId(html, 'empty')).toBeNull();

      testId(html, 'clear-filters')!.click();
      const request = listRequest(http);
      request.flush(residentPage([LIVES, LEFT]));
      await settle(fixture);

      expect(params(request).toString()).toBe('status=active&page=1&pageSize=50');
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
      const { http, html, fixture } = await setup([LIVES], 120);

      expect(text(html, 'page-status')).toBe('Página 1 de 3');
      expect((testId(html, 'previous') as HTMLButtonElement).disabled).toBe(true);

      testId(html, 'next')!.click();
      const next = listRequest(http);
      next.flush(residentPage([LEFT], 120, 2));
      await settle(fixture);
      expect(params(next).get('page')).toBe('2');
      expect(text(html, 'page-status')).toBe('Página 2 de 3');

      testId(html, 'previous')!.click();
      const previous = listRequest(http);
      previous.flush(residentPage([LIVES], 120, 1));
      await settle(fixture);
      expect(params(previous).get('page')).toBe('1');
    });

    it('desabilita "Próxima" na última página', async () => {
      const { http, html, fixture } = await setup([LIVES], 120);

      for (const page of [2, 3]) {
        testId(html, 'next')!.click();
        listRequest(http).flush(residentPage([LIVES], 120, page));
        await settle(fixture);
      }

      expect((testId(html, 'next') as HTMLButtonElement).disabled).toBe(true);
    });
  });
});
