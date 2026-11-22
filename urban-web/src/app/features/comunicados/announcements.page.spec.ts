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
import { Announcement } from '../../core/announcements/announcements.models';
import { provideUrbanIcons } from '../../core/theme/icons';
import { Unit } from '../../core/units/units.models';
import { announcement, announcementPage } from '../../testing/announcements.fixtures';
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
import { unit, unitPage } from '../../testing/units.fixtures';
import { AnnouncementsPage } from './announcements.page';

const FORM = 'urban-announcement-dialog';
const DELETE = 'urban-delete-announcement-dialog';

const UNIT_A = unit({ block: 'A', number: '101' });
const UNIT_B = unit({ block: 'B', number: '202' });
const FIRST = announcement({
  title: 'Reunião de sábado',
  body: 'Às 10h no salão.\nTraga a sua opinião.',
  category: 'important',
  scope: 'condominium',
  createdAt: '2026-11-22T13:50:00Z',
  recipients: 40,
  readers: 12,
  version: '41',
});
const SECOND = announcement({
  title: 'Água cortada',
  body: 'Na terça, das 8h às 12h.',
  category: 'maintenance',
  scope: 'block',
  block: 'B',
  createdAt: '2026-11-20T12:00:00Z',
  updatedAt: '2026-11-21T15:30:00Z',
  recipients: 1,
  readers: 1,
});
const THIRD = announcement({
  title: 'Encomenda na portaria',
  scope: 'unit',
  unitId: UNIT_B.id,
  recipients: 0,
  readers: 0,
});

interface Options {
  items?: Announcement[];
  total?: number;
  units?: Unit[];
}

interface Harness {
  fixture: ComponentFixture<AnnouncementsPage>;
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
  const fixture = TestBed.createComponent(AnnouncementsPage);
  fixture.detectChanges();
  if (options !== 'pending') {
    const items = options.items ?? [FIRST, SECOND, THIRD];
    listRequest(http).flush(announcementPage(items, options.total ?? items.length));
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
  return http.expectOne(
    (request) => request.url === '/api/announcements' && request.method === 'GET',
  );
}

function unitsRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((request) => request.url === '/api/units');
}

function params(request: TestRequest): URLSearchParams {
  return new URLSearchParams(request.request.params.toString());
}

function testId(root: ParentNode, id: string): HTMLElement | null {
  return root.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function text(root: ParentNode, id: string): string {
  return testId(root, id)?.textContent?.replace(/\s+/g, ' ').trim() ?? '';
}

function rows(html: HTMLElement): HTMLElement[] {
  return [...html.querySelectorAll<HTMLElement>('[data-testid="announcement-row"]')];
}

function button(root: ParentNode, label: string): HTMLButtonElement {
  return root.querySelector<HTMLButtonElement>(`button[aria-label="${label}"]`)!;
}

function failure(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

function answerRefresh(http: HttpTestingController, items: Announcement[] = [FIRST]): void {
  listRequest(http).flush(announcementPage(items));
}

function deleteRequest(http: HttpTestingController, target: Announcement): TestRequest {
  return http.expectOne(
    (request) => request.url === `/api/announcements/${target.id}` && request.method === 'DELETE',
  );
}

describe('AnnouncementsPage', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    removeOverlays();
    TestBed.inject(HttpTestingController).match(() => true);
  });

  describe('ao abrir', () => {
    it('mostra que está carregando enquanto a lista não chega, sem oferecer o botão de novo', async () => {
      const { html } = await setup('pending');

      expect(html.querySelector('h1')?.textContent?.trim()).toBe('Comunicados');
      expect(testId(html, 'loading')?.getAttribute('role')).toBe('status');
      expect(text(html, 'loading')).toBe('Carregando os comunicados');
      expect(testId(html, 'new-announcement')).toBeNull();
    });

    it('pede a primeira página de 20 comunicados e as unidades ativas', async () => {
      const { http } = await setup('pending');

      expect(params(listRequest(http)).toString()).toBe('page=1&pageSize=20');
      expect(params(unitsRequest(http)).toString()).toBe('status=active&page=1&pageSize=200');
    });

    it('mostra o erro e deixa tentar de novo quando a busca falha', async () => {
      const { http, html, fixture } = await setup('pending');
      listRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      unitsRequest(http).flush(unitPage([UNIT_A]));
      await settle(fixture);

      expect(text(html, 'load-error')).toContain('Não foi possível carregar os comunicados');
      expect(testId(html, 'new-announcement')).toBeNull();

      testId(html, 'retry')!.click();
      listRequest(http).flush(announcementPage([FIRST]));
      unitsRequest(http).flush(unitPage([UNIT_A]));
      await settle(fixture);

      expect(testId(html, 'load-error')).toBeNull();
      expect(rows(html)).toHaveLength(1);
    });
  });

  describe('lista', () => {
    it('mostra uma linha por comunicado, na ordem que a API mandou', async () => {
      const { html } = await setup();

      expect(rows(html).map((row) => text(row, 'title'))).toEqual([
        'Reunião de sábado',
        'Água cortada',
        'Encomenda na portaria',
      ]);
    });

    it('mostra categoria, alcance, texto e a data de publicação de cada um', async () => {
      const { html } = await setup();

      const [first, second, third] = rows(html);
      expect(text(first, 'category')).toBe('Importante');
      expect(text(first, 'scope')).toBe('Condomínio inteiro');
      expect(text(first, 'body')).toBe('Às 10h no salão. Traga a sua opinião.');
      expect(text(first, 'published')).toBe('Publicado em 22/11/2026 às 10:50');
      expect(text(second, 'category')).toBe('Manutenção');
      expect(text(second, 'scope')).toBe('Bloco B');
      expect(text(third, 'scope')).toBe('Unidade 202 do bloco B');
    });

    it('guarda as quebras de linha do texto', async () => {
      const { html } = await setup();

      expect(testId(rows(html)[0], 'body')!.classList.contains('whitespace-pre-line')).toBe(true);
    });

    it('só mostra "editado em" quando o comunicado foi editado', async () => {
      const { html } = await setup();

      const [first, second] = rows(html);
      expect(testId(first, 'edited')).toBeNull();
      expect(text(second, 'edited')).toBe('Editado em 21/11/2026 às 12:30');
    });

    it('mostra quantos já leram, no singular, no plural e quando não há para quem ler', async () => {
      const { html } = await setup();

      const [first, second, third] = rows(html);
      expect(text(first, 'readers')).toBe('12 de 40 leram');
      expect(text(second, 'readers')).toBe('1 de 1 leu');
      expect(text(third, 'readers')).toBe('Ninguém mora aqui ainda para ler');
    });

    it('cada comunicado pode ser editado e excluído, com o título no rótulo do botão', async () => {
      const { html } = await setup();

      const labels = [...html.querySelectorAll('button[aria-label]')].map((item) =>
        item.getAttribute('aria-label'),
      );
      expect(labels).toEqual(
        expect.arrayContaining([
          'Editar Reunião de sábado',
          'Excluir Reunião de sábado',
          'Editar Água cortada',
          'Excluir Água cortada',
        ]),
      );
      expect(button(html, 'Excluir Água cortada').classList.contains('urban-danger')).toBe(true);
    });

    it('conta os comunicados no plural e no singular', async () => {
      const first = await setup({ items: [FIRST, SECOND] });
      expect(text(first.html, 'total')).toBe('2 comunicados');
      TestBed.resetTestingModule();
      removeOverlays();

      const second = await setup({ items: [FIRST] });
      expect(text(second.html, 'total')).toBe('1 comunicado');
    });

    it('uma unidade que saiu da lista das ativas aparece sem número', async () => {
      const { html } = await setup({ items: [THIRD], units: [UNIT_A] });

      expect(text(rows(html)[0], 'scope')).toBe('Uma unidade que não está mais ativa');
    });

    it('sem nenhum comunicado, convida a publicar o primeiro', async () => {
      const { html } = await setup({ items: [] });

      expect(text(html, 'empty')).toContain('Nenhum comunicado publicado ainda');
      expect(testId(html, 'new-announcement')).not.toBeNull();
      expect(rows(html)).toHaveLength(0);
      expect(testId(html, 'pagination')).toBeNull();
    });
  });

  describe('paginação', () => {
    it('some quando cabe tudo numa página', async () => {
      const { html } = await setup({ total: 20 });

      expect(testId(html, 'pagination')).toBeNull();
    });

    it('mostra a página atual e navega para a próxima e para a anterior', async () => {
      const { http, html, fixture } = await setup({ items: [FIRST], total: 45 });
      expect(text(html, 'page-status')).toBe('Página 1 de 3');
      expect((testId(html, 'previous') as HTMLButtonElement).disabled).toBe(true);

      testId(html, 'next')!.click();
      const next = listRequest(http);
      expect(params(next).get('page')).toBe('2');
      next.flush(announcementPage([SECOND], 45, 2));
      await settle(fixture);

      expect(text(html, 'page-status')).toBe('Página 2 de 3');
      expect(rows(html).map((row) => text(row, 'title'))).toEqual(['Água cortada']);
      expect((testId(html, 'previous') as HTMLButtonElement).disabled).toBe(false);

      testId(html, 'previous')!.click();
      const previous = listRequest(http);
      expect(params(previous).get('page')).toBe('1');
      previous.flush(announcementPage([FIRST], 45, 1));
      await settle(fixture);

      expect(text(html, 'page-status')).toBe('Página 1 de 3');
    });

    it('na última página, "Próxima" fica desabilitada', async () => {
      const { http, html, fixture } = await setup({ items: [FIRST], total: 21 });

      testId(html, 'next')!.click();
      listRequest(http).flush(announcementPage([SECOND], 21, 2));
      await settle(fixture);

      expect((testId(html, 'next') as HTMLButtonElement).disabled).toBe(true);
    });
  });

  describe('publicar', () => {
    it('abre o diálogo, grava e atualiza a lista, avisando o que foi feito', async () => {
      const { http, html, fixture } = await setup({ items: [FIRST] });

      testId(html, 'new-announcement')!.click();
      TestBed.tick();
      typeIn(FORM, 'title', 'Limpeza da caixa d’água');
      typeIn(FORM, 'body', 'Sem água das 8h às 12h.');
      chooseIn(FORM, 'category', 'maintenance');
      dialogOf(FORM).querySelector<HTMLInputElement>('input[type="radio"][value="block"]')!.click();
      TestBed.tick();
      chooseIn(FORM, 'block', 'A');
      submitDialog(FORM);
      await flushPromises();

      const post = http.expectOne('/api/announcements');
      expect(post.request.method).toBe('POST');
      expect(post.request.body).toEqual({
        title: 'Limpeza da caixa d’água',
        body: 'Sem água das 8h às 12h.',
        category: 'maintenance',
        scope: 'block',
        block: 'A',
        unitId: null,
      });
      post.flush(announcement());
      await flushPromises();
      answerRefresh(http, [SECOND, FIRST]);
      await settle(fixture);

      expect(dialogOf(FORM)).toBeNull();
      expect(text(html, 'notice')).toBe('Comunicado "Limpeza da caixa d’água" publicado');
      expect(rows(html)).toHaveLength(2);
    });

    it('explica no diálogo por que a API recusou, sem fechar', async () => {
      const { http, html, fixture } = await setup({ items: [FIRST] });

      testId(html, 'new-announcement')!.click();
      TestBed.tick();
      typeIn(FORM, 'title', 'Aviso');
      typeIn(FORM, 'body', 'Texto');
      submitDialog(FORM);
      await flushPromises();
      http.expectOne('/api/announcements').flush(...failure(422, 'announcement.block.not_found'));
      await settle(fixture);

      expect(alertOf(FORM)).toBe('Este bloco não tem unidades cadastradas');
      expect(dialogOf(FORM)).not.toBeNull();
      expect(testId(html, 'notice')).toBeNull();
    });
  });

  describe('editar', () => {
    it('abre com os dados do comunicado e grava com a versão lida', async () => {
      const { http, html, fixture } = await setup({ items: [FIRST] });

      button(html, 'Editar Reunião de sábado').click();
      TestBed.tick();
      expect(
        (dialogOf(FORM).querySelector('[formControlName="title"]') as HTMLInputElement).value,
      ).toBe('Reunião de sábado');
      typeIn(FORM, 'title', 'Reunião de domingo');
      TestBed.tick();
      submitDialog(FORM);
      await flushPromises();

      const put = http.expectOne(`/api/announcements/${FIRST.id}`);
      expect(put.request.method).toBe('PUT');
      expect(put.request.body).toEqual({
        title: 'Reunião de domingo',
        body: FIRST.body,
        category: 'important',
        scope: 'condominium',
        block: null,
        unitId: null,
        version: '41',
      });
      put.flush(FIRST);
      await flushPromises();
      answerRefresh(http, [FIRST]);
      await settle(fixture);

      expect(dialogOf(FORM)).toBeNull();
      expect(text(html, 'notice')).toBe('Comunicado "Reunião de domingo" atualizado');
    });

    it('quando outra pessoa mexeu antes, avisa no diálogo e atualiza a lista por trás', async () => {
      const { http, html, fixture } = await setup({ items: [FIRST] });

      button(html, 'Editar Reunião de sábado').click();
      TestBed.tick();
      typeIn(FORM, 'title', 'Reunião de domingo');
      TestBed.tick();
      submitDialog(FORM);
      await flushPromises();
      http
        .expectOne(`/api/announcements/${FIRST.id}`)
        .flush(...failure(409, 'concurrency.conflict'));
      await flushPromises();
      answerRefresh(http, [announcement({ title: 'Reunião de sábado (nova)' })]);
      await settle(fixture);

      expect(alertOf(FORM)).toContain('foi alterado em outro lugar');
      expect(text(rows(html)[0], 'title')).toBe('Reunião de sábado (nova)');
    });
  });

  describe('excluir', () => {
    it('pede confirmação num diálogo e explica que não se desfaz', async () => {
      const { http, html } = await setup({ items: [FIRST] });

      button(html, 'Excluir Reunião de sábado').click();
      TestBed.tick();

      expect(dialogOf(DELETE).textContent).toContain('Excluir Reunião de sábado');
      expect(dialogOf(DELETE).textContent).toContain('não se desfaz');
      http.expectNone(
        (request) => request.url.startsWith('/api/announcements') && request.method !== 'GET',
      );
    });

    it('cancelar fecha o diálogo sem chamar a API', async () => {
      const { http, html } = await setup({ items: [FIRST] });

      button(html, 'Excluir Reunião de sábado').click();
      TestBed.tick();
      dialogOf(DELETE).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();
      await flushPromises();
      TestBed.tick();

      expect(dialogOf(DELETE)).toBeNull();
      http.expectNone(
        (request) => request.url.startsWith('/api/announcements') && request.method !== 'GET',
      );
    });

    it('confirmar exclui com a versão lida, atualiza a lista e avisa', async () => {
      const { http, html, fixture } = await setup({ items: [FIRST, SECOND] });

      button(html, 'Excluir Reunião de sábado').click();
      TestBed.tick();
      submitDialog(DELETE);
      await flushPromises();

      const request = deleteRequest(http, FIRST);
      expect(request.request.params.get('version')).toBe('41');
      request.flush(null, { status: 204, statusText: 'No Content' });
      await flushPromises();
      answerRefresh(http, [SECOND]);
      await settle(fixture);

      expect(dialogOf(DELETE)).toBeNull();
      expect(text(html, 'notice')).toBe('Comunicado "Reunião de sábado" excluído');
      expect(rows(html).map((row) => text(row, 'title'))).toEqual(['Água cortada']);
    });

    it('comunicado que já não existe: explica no diálogo e atualiza a lista', async () => {
      const { http, html, fixture } = await setup({ items: [FIRST, SECOND] });

      button(html, 'Excluir Reunião de sábado').click();
      TestBed.tick();
      submitDialog(DELETE);
      await flushPromises();
      deleteRequest(http, FIRST).flush(...failure(404, 'announcement.not_found'));
      await flushPromises();
      answerRefresh(http, [SECOND]);
      await settle(fixture);

      expect(alertOf(DELETE)).toContain('já foi apagado');
      expect(rows(html)).toHaveLength(1);
    });
  });
});
