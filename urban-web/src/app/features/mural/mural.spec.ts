import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { ComponentFixture, TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { MyAnnouncement, MyAnnouncementPage } from '../../core/announcements/announcements.models';
import { NOW } from '../../core/finance/clock';
import { provideUrbanIcons } from '../../core/theme/icons';
import { myAnnouncement, myAnnouncementPage } from '../../testing/announcements.fixtures';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { Mural } from './mural';

const CLOCK = new Date('2026-11-22T15:00:00Z');
const MINE = '/api/announcements/mine';
const READ_ALL = '/api/announcements/read-all';
const UNREAD = { general: 2, maintenance: 1, important: 1 };

interface Harness {
  fixture: ComponentFixture<Mural>;
  http: HttpTestingController;
  html: HTMLElement;
}

function setup(): Harness {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      provideUrbanIcons(),
      { provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } },
      { provide: NOW, useValue: () => CLOCK },
    ],
  });
  const fixture = TestBed.createComponent(Mural);
  fixture.detectChanges();
  return { fixture, http: TestBed.inject(HttpTestingController), html: fixture.nativeElement };
}

async function settle(fixture: ComponentFixture<unknown>): Promise<void> {
  for (let round = 0; round < 3; round++) {
    await flushPromises();
    TestBed.tick();
    fixture.detectChanges();
    await fixture.whenStable();
  }
}

function mine(http: HttpTestingController): TestRequest {
  return http.expectOne((request) => request.url === MINE && request.method === 'GET');
}

function params(request: TestRequest): string {
  return new URLSearchParams(request.request.params.toString()).toString();
}

/** Abre o mural e responde à primeira busca. */
async function opened(
  items: MyAnnouncement[] = [myAnnouncement(), myAnnouncement()],
  extra: Partial<MyAnnouncementPage> = { unread: 4, unreadByCategory: UNREAD },
): Promise<Harness> {
  const harness = setup();
  mine(harness.http).flush(myAnnouncementPage(items, extra));
  await settle(harness.fixture);
  return harness;
}

function byId(html: ParentNode, id: string): HTMLElement | null {
  return html.querySelector<HTMLElement>(`[data-testid="${id}"]`);
}

function allById(html: ParentNode, id: string): HTMLElement[] {
  return [...html.querySelectorAll<HTMLElement>(`[data-testid="${id}"]`)];
}

function textOf(element: Element | null): string {
  return (element?.textContent ?? '').replace(/\s+/g, ' ').trim();
}

async function click(harness: Harness, id: string): Promise<void> {
  byId(harness.html, id)!.click();
  await settle(harness.fixture);
}

describe('Mural', () => {
  afterEach(() => {
    const http = TestBed.inject(HttpTestingController);
    expect(http.match((request) => request.url.startsWith('/api/'))).toEqual([]);
  });

  it('abre buscando a primeira página, com o título visível e um esqueleto enquanto espera', () => {
    const { http, html } = setup();

    const request = mine(http);
    expect(params(request)).toBe('page=1&pageSize=10');
    expect(html.querySelector('h1')?.textContent?.trim()).toBe('Mural');
    expect(html.querySelector('h1')?.classList).not.toContain('sr-only');
    const loading = byId(html, 'loading')!;
    expect(loading.getAttribute('role')).toBe('status');
    expect(byId(html, 'mural-card')).toBeNull();
    request.flush(myAnnouncementPage([]));
  });

  describe('resumo', () => {
    it('diz quantos avisos novos o morador tem', async () => {
      const { html } = await opened();

      expect(textOf(byId(html, 'summary'))).toContain('Você tem 4 avisos novos');
    });

    it('diz que o morador está em dia quando não há novo', async () => {
      const { html } = await opened(
        [myAnnouncement({ read: true, readAt: '2026-11-22T14:00:00Z' })],
        {
          unread: 0,
        },
      );

      expect(textOf(byId(html, 'summary'))).toContain('Você está em dia');
    });
  });

  describe('lista', () => {
    it('mostra um cartão por aviso, na ordem em que a API mandou, e quantos já aparecem', async () => {
      const first = myAnnouncement({ title: 'Primeiro' });
      const second = myAnnouncement({
        title: 'Segundo',
        read: true,
        readAt: '2026-11-22T14:00:00Z',
      });
      const { html } = await opened([first, second], { total: 25, unread: 1 });

      expect(allById(html, 'mural-card').map((card) => textOf(card.querySelector('h3')))).toEqual([
        'Primeiro',
        'Segundo',
      ]);
      expect(textOf(byId(html, 'shown-count'))).toBe('Mostrando 2 de 25 avisos');
    });

    it('chama a quantidade de singular quando é só um aviso', async () => {
      const { html } = await opened([myAnnouncement()], { total: 1, unread: 1 });

      expect(textOf(byId(html, 'shown-count'))).toBe('Mostrando 1 de 1 aviso');
    });

    it('explica quando não há nenhum aviso, sem oferecer filtro', async () => {
      const { html } = await opened([], { unread: 0 });

      expect(textOf(byId(html, 'empty'))).toContain('Nenhum aviso por enquanto');
      expect(byId(html, 'mural-card')).toBeNull();
      expect(byId(html, 'empty-filter')).toBeNull();
    });
  });

  describe('erro', () => {
    it('mostra o erro com "Tentar de novo", que busca outra vez', async () => {
      const { fixture, http, html } = setup();
      mine(http).flush(problem(500, 'erro'), { status: 500, statusText: 'Erro' });
      await settle(fixture);

      expect(byId(html, 'loading')).toBeNull();
      const error = byId(html, 'load-error')!;
      expect(textOf(error)).toContain('Não foi possível carregar os avisos');
      expect(error.querySelector('mat-icon')).not.toBeNull();

      byId(html, 'retry')!.click();
      mine(http).flush(myAnnouncementPage([myAnnouncement()], { unread: 1 }));
      await settle(fixture);

      expect(byId(html, 'load-error')).toBeNull();
      expect(allById(html, 'mural-card')).toHaveLength(1);
    });
  });

  describe('filtros por categoria', () => {
    it('mostra Todos e as três categorias, cada uma com o seu número de não lidos, e Todos marcado', async () => {
      const { html } = await opened();

      expect(
        ['chip-all', 'chip-general', 'chip-maintenance', 'chip-important'].map((id) =>
          textOf(byId(html, id)),
        ),
      ).toEqual(['Todos 4', 'Geral 2', 'Manutenção 1', 'Importante 1']);
      expect(byId(html, 'chip-all')!.getAttribute('aria-pressed')).toBe('true');
      expect(byId(html, 'chip-general')!.getAttribute('aria-pressed')).toBe('false');
      expect(byId(html, 'chip-maintenance')!.closest('[role="group"]')).not.toBeNull();
    });

    it('não mostra a bolinha de uma categoria sem não lidos', async () => {
      const { html } = await opened([myAnnouncement()], {
        unread: 1,
        unreadByCategory: { general: 1, maintenance: 0, important: 0 },
      });

      expect(
        byId(html, 'chip-maintenance')!.querySelector('[data-testid="chip-badge"]'),
      ).toBeNull();
      expect(
        byId(html, 'chip-general')!.querySelector('[data-testid="chip-badge"]'),
      ).not.toBeNull();
    });

    it('clicar numa categoria busca só os avisos dela e a marca como escolhida', async () => {
      const harness = await opened();
      const maintenance = myAnnouncement({ title: 'Obra no telhado', category: 'maintenance' });

      byId(harness.html, 'chip-maintenance')!.click();
      const request = mine(harness.http);
      request.flush(myAnnouncementPage([maintenance], { unread: 4, unreadByCategory: UNREAD }));
      await settle(harness.fixture);

      expect(params(request)).toBe('page=1&pageSize=10&category=maintenance');
      expect(byId(harness.html, 'chip-maintenance')!.getAttribute('aria-pressed')).toBe('true');
      expect(byId(harness.html, 'chip-all')!.getAttribute('aria-pressed')).toBe('false');
      expect(
        allById(harness.html, 'mural-card').map((card) => textOf(card.querySelector('h3'))),
      ).toEqual(['Obra no telhado']);
    });

    it('"Só não lidos" busca só os que faltam ler e fica marcado', async () => {
      const harness = await opened();

      const toggle = byId(harness.html, 'unread-only')!;
      expect(toggle.getAttribute('aria-pressed')).toBe('false');
      toggle.click();
      const request = mine(harness.http);
      request.flush(
        myAnnouncementPage([myAnnouncement()], { unread: 4, unreadByCategory: UNREAD }),
      );
      await settle(harness.fixture);

      expect(params(request)).toBe('page=1&pageSize=10&unreadOnly=true');
      expect(byId(harness.html, 'unread-only')!.getAttribute('aria-pressed')).toBe('true');
    });

    it('sem resultado no filtro, diz isso e "Ver todos os avisos" limpa os filtros', async () => {
      const harness = await opened();

      byId(harness.html, 'chip-important')!.click();
      mine(harness.http).flush(myAnnouncementPage([], { unread: 4, unreadByCategory: UNREAD }));
      await settle(harness.fixture);

      expect(textOf(byId(harness.html, 'empty-filter'))).toContain('Nenhum aviso neste filtro');
      expect(byId(harness.html, 'empty')).toBeNull();
      expect(byId(harness.html, 'chip-important')!.getAttribute('aria-pressed')).toBe('true');

      byId(harness.html, 'clear-filters')!.click();
      const request = mine(harness.http);
      request.flush(
        myAnnouncementPage([myAnnouncement()], { unread: 4, unreadByCategory: UNREAD }),
      );
      await settle(harness.fixture);

      expect(params(request)).toBe('page=1&pageSize=10');
      expect(byId(harness.html, 'empty-filter')).toBeNull();
      expect(byId(harness.html, 'chip-all')!.getAttribute('aria-pressed')).toBe('true');
    });

    it('"Só não lidos" sem nenhum não lido diz que está tudo lido', async () => {
      const harness = await opened(
        [myAnnouncement({ read: true, readAt: '2026-11-22T14:00:00Z' })],
        { unread: 0 },
      );

      byId(harness.html, 'unread-only')!.click();
      mine(harness.http).flush(myAnnouncementPage([], { unread: 0 }));
      await settle(harness.fixture);

      expect(textOf(byId(harness.html, 'empty-filter'))).toContain('Você já leu todos os avisos');
    });
  });

  describe('marcar como lido', () => {
    it('marca o cartão na hora e diminui o resumo, sem buscar a lista de novo', async () => {
      const target = myAnnouncement({ category: 'general' });
      const harness = await opened([target, myAnnouncement()]);

      byId(harness.html, 'mark-read')!.click();
      harness.fixture.detectChanges();

      const request = harness.http.expectOne(`/api/announcements/${target.id}/read`);
      expect(request.request.method).toBe('POST');
      expect(allById(harness.html, 'mural-card')[0].getAttribute('data-unread')).toBe('false');
      expect(textOf(byId(harness.html, 'summary'))).toContain('Você tem 3 avisos novos');
      expect(textOf(byId(harness.html, 'chip-general'))).toBe('Geral 1');
      request.flush(null, { status: 204, statusText: 'No Content' });
      await settle(harness.fixture);
      expect(
        textOf(allById(harness.html, 'mural-card')[0].querySelector('[data-testid="read-state"]')),
      ).toContain('Lido em');
    });

    it('desfaz e avisa em vermelho quando a API recusa', async () => {
      const target = myAnnouncement();
      const harness = await opened([target]);

      byId(harness.html, 'mark-read')!.click();
      harness.http
        .expectOne(`/api/announcements/${target.id}/read`)
        .flush(problem(500, 'erro'), { status: 500, statusText: 'Erro' });
      await settle(harness.fixture);

      expect(allById(harness.html, 'mural-card')[0].getAttribute('data-unread')).toBe('true');
      const error = byId(harness.html, 'action-error')!;
      expect(error.getAttribute('role')).toBe('alert');
      expect(textOf(error)).toContain('Não foi possível marcar o aviso como lido');
      expect(textOf(byId(harness.html, 'summary'))).toContain('Você tem 4 avisos novos');
    });
  });

  describe('marcar todos como lidos', () => {
    it('com Todos escolhido, marca o mural inteiro, avisa quantos e busca a lista de novo', async () => {
      const harness = await opened();
      const button = byId(harness.html, 'mark-all') as HTMLButtonElement;
      expect(textOf(button)).toBe('Marcar todos como lidos');
      expect(button.disabled).toBe(false);

      button.click();
      const request = harness.http.expectOne((r) => r.url === READ_ALL);
      expect(params(request)).toBe('');
      request.flush({ marked: 4 });
      await flushPromises();
      mine(harness.http).flush(
        myAnnouncementPage([myAnnouncement({ read: true, readAt: '2026-11-22T15:00:00Z' })], {
          unread: 0,
        }),
      );
      await settle(harness.fixture);

      const notice = byId(harness.html, 'notice')!;
      expect(notice.getAttribute('role')).toBe('status');
      expect(textOf(notice)).toContain('4 avisos marcados como lidos');
      expect(textOf(byId(harness.html, 'summary'))).toContain('Você está em dia');
      expect((byId(harness.html, 'mark-all') as HTMLButtonElement).disabled).toBe(true);
    });

    it('com uma categoria escolhida, o botão fala dela e marca só os avisos dela', async () => {
      const harness = await opened();
      byId(harness.html, 'chip-important')!.click();
      mine(harness.http).flush(
        myAnnouncementPage([myAnnouncement({ category: 'important' })], {
          unread: 4,
          unreadByCategory: UNREAD,
        }),
      );
      await settle(harness.fixture);

      expect(textOf(byId(harness.html, 'mark-all'))).toBe('Marcar avisos importantes como lidos');
      byId(harness.html, 'mark-all')!.click();
      const request = harness.http.expectOne((r) => r.url === READ_ALL);
      expect(params(request)).toBe('category=important');
      request.flush({ marked: 1 });
      await flushPromises();
      mine(harness.http).flush(myAnnouncementPage([]));
      await settle(harness.fixture);
    });

    it('fica desligado quando não há nenhum não lido no que está na tela', async () => {
      const { html } = await opened(
        [myAnnouncement({ read: true, readAt: '2026-11-22T14:00:00Z' })],
        {
          unread: 0,
        },
      );

      expect((byId(html, 'mark-all') as HTMLButtonElement).disabled).toBe(true);
    });

    it('avisa em vermelho quando a API recusa', async () => {
      const harness = await opened();

      byId(harness.html, 'mark-all')!.click();
      harness.http
        .expectOne((r) => r.url === READ_ALL)
        .flush(problem(500, 'erro'), { status: 500, statusText: 'Erro' });
      await settle(harness.fixture);

      expect(textOf(byId(harness.html, 'action-error'))).toContain(
        'Não foi possível marcar os avisos como lidos',
      );
      expect(byId(harness.html, 'notice')).toBeNull();
    });
  });

  describe('atualizar', () => {
    it('"Atualizar avisos" busca a primeira página de novo', async () => {
      const harness = await opened();
      const button = byId(harness.html, 'refresh')!;
      expect(button.getAttribute('aria-label')).toBe('Atualizar avisos');

      button.click();
      const request = mine(harness.http);
      request.flush(myAnnouncementPage([myAnnouncement({ title: 'Novidade' })], { unread: 5 }));
      await settle(harness.fixture);

      expect(params(request)).toBe('page=1&pageSize=10');
      expect(textOf(allById(harness.html, 'mural-card')[0].querySelector('h3'))).toBe('Novidade');
      expect(textOf(byId(harness.html, 'summary'))).toContain('Você tem 5 avisos novos');
    });
  });

  describe('mostrar mais', () => {
    it('oferece "Mostrar mais avisos" enquanto há o que mostrar e acrescenta a página seguinte', async () => {
      const first = [myAnnouncement({ title: 'Um' }), myAnnouncement({ title: 'Dois' })];
      const harness = await opened(first, { total: 12, unread: 3 });
      expect(textOf(byId(harness.html, 'load-more'))).toBe('Mostrar mais avisos');

      await click(harness, 'load-more');
      const request = mine(harness.http);
      request.flush(
        myAnnouncementPage([myAnnouncement({ title: 'Três' })], { total: 12, page: 2, unread: 3 }),
      );
      await settle(harness.fixture);

      expect(params(request)).toBe('page=2&pageSize=10');
      expect(
        allById(harness.html, 'mural-card').map((card) => textOf(card.querySelector('h3'))),
      ).toEqual(['Um', 'Dois', 'Três']);
      expect(byId(harness.html, 'load-more')).toBeNull();
      expect(textOf(byId(harness.html, 'shown-count'))).toBe('Mostrando 3 de 12 avisos');
    });

    it('não oferece quando os avisos já são todos', async () => {
      const { html } = await opened([myAnnouncement()], { total: 1, unread: 1 });

      expect(byId(html, 'load-more')).toBeNull();
    });

    it('trava o botão e diz "Buscando" enquanto a página chega', async () => {
      const harness = await opened([myAnnouncement()], { total: 12, unread: 1 });

      byId(harness.html, 'load-more')!.click();
      harness.fixture.detectChanges();

      const button = byId(harness.html, 'load-more') as HTMLButtonElement;
      expect(button.disabled).toBe(true);
      expect(textOf(button)).toBe('Buscando');
      mine(harness.http).flush(myAnnouncementPage([], { total: 12, page: 2 }));
      await settle(harness.fixture);
    });
  });
});
