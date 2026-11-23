import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { MyAnnouncement } from '../../core/announcements/announcements.models';
import { MuralUpdate } from '../../core/announcements/mural-live';
import { NOW } from '../../core/finance/clock';
import {
  myAnnouncement,
  myAnnouncementPage,
  NOTHING_UNREAD,
} from '../../testing/announcements.fixtures';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { fakeLive } from '../../testing/mural-live.fake';
import { MuralStore } from './mural.store';

const CLOCK = new Date('2026-11-22T15:00:00Z');
const MINE = '/api/announcements/mine';
const READ_ALL = '/api/announcements/read-all';

type Store = InstanceType<typeof MuralStore>;

function setup(live = fakeLive()): {
  store: Store;
  http: HttpTestingController;
  live: ReturnType<typeof fakeLive>;
} {
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: NOW, useValue: () => CLOCK },
      live.provider,
      MuralStore,
    ],
  });
  return { store: TestBed.inject(MuralStore), http: TestBed.inject(HttpTestingController), live };
}

function params(request: TestRequest): string {
  return new URLSearchParams(request.request.params.toString()).toString();
}

function mineRequest(http: HttpTestingController): TestRequest {
  return http.expectOne((request) => request.url === MINE && request.method === 'GET');
}

function fail(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

const UNREAD = { general: 2, maintenance: 1, important: 1 };

/** Carrega o mural com os avisos dados e as contagens de não lidos. */
async function loaded(
  store: Store,
  http: HttpTestingController,
  items: MyAnnouncement[] = [myAnnouncement(), myAnnouncement()],
  extra: Parameters<typeof myAnnouncementPage>[1] = { unread: 4, unreadByCategory: UNREAD },
): Promise<void> {
  const loading = store.load();
  mineRequest(http).flush(myAnnouncementPage(items, extra));
  await loading;
}

describe('MuralStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('começa carregando, sem avisos, sem filtro e na primeira página', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.items()).toEqual([]);
    expect(store.page()).toBe(1);
    expect(store.category()).toBeNull();
    expect(store.unreadOnly()).toBe(false);
    expect(store.hasMore()).toBe(false);
    expect(store.hasFilters()).toBe(false);
    expect(store.notice()).toBeNull();
    expect(store.actionError()).toBeNull();
  });

  describe('load', () => {
    it('busca a primeira página de 10 e passa a pronto, com as contagens de não lidos', async () => {
      const { store, http } = setup();
      const items = [myAnnouncement(), myAnnouncement()];

      const loading = store.load();
      expect(store.status()).toBe('loading');
      const request = mineRequest(http);
      request.flush(myAnnouncementPage(items, { total: 25, unread: 4, unreadByCategory: UNREAD }));
      await loading;

      expect(params(request)).toBe('page=1&pageSize=10');
      expect(store.status()).toBe('ready');
      expect(store.items()).toEqual(items);
      expect(store.total()).toBe(25);
      expect(store.unread()).toBe(4);
      expect(store.unreadByCategory()).toEqual(UNREAD);
      expect(store.hasMore()).toBe(true);
    });

    it('vai a erro quando a busca falha e tenta de novo', async () => {
      const { store, http } = setup();

      const failing = store.load();
      mineRequest(http).flush(...fail(500, 'erro'));
      await failing;
      expect(store.status()).toBe('error');

      await loaded(store, http);
      expect(store.status()).toBe('ready');
    });
  });

  describe('hasMore', () => {
    it('é falso quando os avisos mostrados já são todos', async () => {
      const { store, http } = setup();

      await loaded(store, http, [myAnnouncement()], { total: 1 });

      expect(store.hasMore()).toBe(false);
    });
  });

  describe('chips', () => {
    it('lista Todos e as três categorias, cada uma com os seus não lidos', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      expect(store.chips()).toEqual([
        { value: null, label: 'Todos', unread: 4 },
        { value: 'general', label: 'Geral', unread: 2 },
        { value: 'maintenance', label: 'Manutenção', unread: 1 },
        { value: 'important', label: 'Importante', unread: 1 },
      ]);
    });
  });

  describe('filtros', () => {
    it('trocar a categoria busca a primeira página dela e troca a lista, sem voltar ao carregando', async () => {
      const { store, http } = setup();
      await loaded(store, http);
      const filtered = [myAnnouncement({ category: 'maintenance' })];

      const changing = store.setCategory('maintenance');
      expect(store.category()).toBe('maintenance');
      expect(store.status()).toBe('ready');
      expect(store.refreshing()).toBe(true);
      const request = mineRequest(http);
      request.flush(myAnnouncementPage(filtered, { unread: 4, unreadByCategory: UNREAD }));
      await changing;

      expect(params(request)).toBe('page=1&pageSize=10&category=maintenance');
      expect(store.items()).toEqual(filtered);
      expect(store.refreshing()).toBe(false);
      expect(store.hasFilters()).toBe(true);
    });

    it('não busca de novo quando a categoria pedida já é a escolhida', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      await store.setCategory(null);

      http.expectNone((request) => request.url === MINE);
    });

    it('"só não lidos" manda unreadOnly e vale junto com a categoria', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const first = store.setCategory('important');
      mineRequest(http).flush(myAnnouncementPage([]));
      await first;
      const second = store.setUnreadOnly(true);
      const request = mineRequest(http);
      request.flush(myAnnouncementPage([]));
      await second;

      expect(params(request)).toBe('page=1&pageSize=10&unreadOnly=true&category=important');
      expect(store.unreadOnly()).toBe(true);
    });

    it('limpar os filtros volta a Todos e a todos os avisos, numa busca só', async () => {
      const { store, http } = setup();
      await loaded(store, http);
      const byCategory = store.setCategory('general');
      mineRequest(http).flush(myAnnouncementPage([]));
      await byCategory;
      const byRead = store.setUnreadOnly(true);
      mineRequest(http).flush(myAnnouncementPage([]));
      await byRead;

      const clearing = store.clearFilters();
      const request = mineRequest(http);
      request.flush(myAnnouncementPage([myAnnouncement()]));
      await clearing;

      expect(params(request)).toBe('page=1&pageSize=10');
      expect(store.category()).toBeNull();
      expect(store.unreadOnly()).toBe(false);
      expect(store.hasFilters()).toBe(false);
    });

    it('limpar sem filtro nenhum não busca', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      await store.clearFilters();

      http.expectNone((request) => request.url === MINE);
    });

    it('descarta a resposta de um filtro que já foi trocado por outro', async () => {
      const { store, http } = setup();
      await loaded(store, http);
      const slow = [myAnnouncement({ title: 'Manutenção' })];
      const fast = [myAnnouncement({ title: 'Importante' })];

      const first = store.setCategory('maintenance');
      const second = store.setCategory('important');
      const [firstRequest, secondRequest] = http.match((request) => request.url === MINE);
      secondRequest.flush(myAnnouncementPage(fast));
      await second;
      firstRequest.flush(myAnnouncementPage(slow));
      await first;

      expect(store.items()).toEqual(fast);
      expect(store.category()).toBe('important');
      expect(store.refreshing()).toBe(false);
    });

    it('vai a erro quando a busca do filtro falha, e "tentar de novo" usa o filtro escolhido', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const changing = store.setCategory('general');
      mineRequest(http).flush(...fail(500, 'erro'));
      await changing;
      expect(store.status()).toBe('error');

      const retrying = store.load();
      const request = mineRequest(http);
      request.flush(myAnnouncementPage([]));
      await retrying;

      expect(params(request)).toBe('page=1&pageSize=10&category=general');
      expect(store.status()).toBe('ready');
    });
  });

  describe('refresh', () => {
    it('busca a primeira página de novo, com os filtros de agora', async () => {
      const { store, http } = setup();
      await loaded(store, http);
      const filtering = store.setUnreadOnly(true);
      mineRequest(http).flush(myAnnouncementPage([]));
      await filtering;

      const refreshing = store.refresh();
      const request = mineRequest(http);
      const fresh = [myAnnouncement()];
      request.flush(myAnnouncementPage(fresh, { unread: 1 }));
      await refreshing;

      expect(params(request)).toBe('page=1&pageSize=10&unreadOnly=true');
      expect(store.items()).toEqual(fresh);
      expect(store.unread()).toBe(1);
      expect(store.page()).toBe(1);
    });
  });

  describe('loadMore', () => {
    it('busca a página seguinte e acrescenta ao fim, sem repetir o que já está na lista', async () => {
      const { store, http } = setup();
      const first = [myAnnouncement(), myAnnouncement()];
      await loaded(store, http, first, { total: 12, unread: 3 });
      const next = myAnnouncement();

      const loading = store.loadMore();
      expect(store.loadingMore()).toBe(true);
      const request = mineRequest(http);
      request.flush(myAnnouncementPage([first[1], next], { total: 12, page: 2, unread: 2 }));
      await loading;

      expect(params(request)).toBe('page=2&pageSize=10');
      expect(store.items()).toEqual([...first, next]);
      expect(store.page()).toBe(2);
      expect(store.unread()).toBe(2);
      expect(store.loadingMore()).toBe(false);
      expect(store.hasMore()).toBe(false);
    });

    it('leva os filtros para a página seguinte', async () => {
      const { store, http } = setup();
      await loaded(store, http);
      const filtering = store.setCategory('general');
      mineRequest(http).flush(myAnnouncementPage([myAnnouncement()], { total: 11 }));
      await filtering;

      const loading = store.loadMore();
      const request = mineRequest(http);
      request.flush(myAnnouncementPage([], { total: 11, page: 2 }));
      await loading;

      expect(params(request)).toBe('page=2&pageSize=10&category=general');
    });

    it('não busca quando os avisos mostrados já são todos', async () => {
      const { store, http } = setup();
      await loaded(store, http, [myAnnouncement()], { total: 1 });

      await store.loadMore();

      http.expectNone((request) => request.url === MINE);
    });

    it('não busca de novo enquanto a página seguinte ainda está chegando', async () => {
      const { store, http } = setup();
      await loaded(store, http, [myAnnouncement()], { total: 20 });

      const first = store.loadMore();
      const second = store.loadMore();
      mineRequest(http).flush(myAnnouncementPage([], { total: 20, page: 2 }));
      await Promise.all([first, second]);
    });

    it('mantém a lista e avisa quando a busca falha, deixando tentar de novo', async () => {
      const { store, http } = setup();
      const items = [myAnnouncement(), myAnnouncement()];
      await loaded(store, http, items, { total: 12 });

      const loading = store.loadMore();
      mineRequest(http).flush(...fail(500, 'erro'));
      await loading;

      expect(store.items()).toEqual(items);
      expect(store.status()).toBe('ready');
      expect(store.loadingMore()).toBe(false);
      expect(store.actionError()).toBe('Não foi possível carregar mais avisos. Tente de novo.');
    });

    it('descarta a página que chegou depois de o filtro mudar', async () => {
      const { store, http } = setup();
      await loaded(store, http, [myAnnouncement()], { total: 20 });
      const fresh = [myAnnouncement({ category: 'important' })];

      const more = store.loadMore();
      const filtering = store.setCategory('important');
      const [moreRequest, filterRequest] = http.match((request) => request.url === MINE);
      filterRequest.flush(myAnnouncementPage(fresh));
      await filtering;
      moreRequest.flush(myAnnouncementPage([myAnnouncement()], { total: 20, page: 2 }));
      await more;

      expect(store.items()).toEqual(fresh);
      expect(store.page()).toBe(1);
    });
  });

  describe('markRead', () => {
    it('marca na hora, antes de a API responder: lido, com a hora de agora, e um não lido a menos na categoria', async () => {
      const { store, http } = setup();
      const target = myAnnouncement({ category: 'maintenance' });
      await loaded(store, http, [target, myAnnouncement()]);

      const marking = store.markRead(target);

      const marked = store.items().find((item) => item.id === target.id)!;
      expect(marked.read).toBe(true);
      expect(marked.readAt).toBe(CLOCK.toISOString());
      expect(store.unread()).toBe(3);
      expect(store.unreadByCategory()).toEqual({ general: 2, maintenance: 0, important: 1 });
      expect(store.marking()).toEqual([target.id]);
      const request = http.expectOne(`/api/announcements/${target.id}/read`);
      expect(request.request.method).toBe('POST');
      request.flush(null, { status: 204, statusText: 'No Content' });
      await marking;

      expect(store.marking()).toEqual([]);
      expect(store.items()).toHaveLength(2);
      expect(store.actionError()).toBeNull();
    });

    it('não chama a API para um aviso que já está lido nem para um que já está sendo marcado', async () => {
      const { store, http } = setup();
      const already = myAnnouncement({ read: true, readAt: '2026-11-21T10:00:00Z' });
      const target = myAnnouncement();
      await loaded(store, http, [already, target]);

      await store.markRead(already);
      const first = store.markRead(target);
      const second = store.markRead(target);
      http
        .expectOne(`/api/announcements/${target.id}/read`)
        .flush(null, { status: 204, statusText: 'No Content' });
      await Promise.all([first, second]);

      expect(store.unread()).toBe(3);
    });

    it('desfaz a marcação e avisa quando a API recusa', async () => {
      const { store, http } = setup();
      const target = myAnnouncement({ category: 'general' });
      await loaded(store, http, [target]);

      const marking = store.markRead(target);
      http.expectOne(`/api/announcements/${target.id}/read`).flush(...fail(500, 'erro'));
      await marking;

      const restored = store.items()[0];
      expect(restored.read).toBe(false);
      expect(restored.readAt).toBeNull();
      expect(store.unread()).toBe(4);
      expect(store.unreadByCategory()).toEqual(UNREAD);
      expect(store.marking()).toEqual([]);
      expect(store.actionError()).toBe('Não foi possível marcar o aviso como lido. Tente de novo.');
    });

    it('quando o aviso sumiu (404), explica e busca o mural de novo', async () => {
      const { store, http } = setup();
      const target = myAnnouncement();
      await loaded(store, http, [target]);

      const marking = store.markRead(target);
      http
        .expectOne(`/api/announcements/${target.id}/read`)
        .flush(...fail(404, 'announcement.not_found'));
      await flushPromises();
      const fresh = [myAnnouncement()];
      mineRequest(http).flush(myAnnouncementPage(fresh, { unread: 1 }));
      await marking;

      expect(store.actionError()).toBe('Este aviso não está mais disponível.');
      expect(store.items()).toEqual(fresh);
      expect(store.unread()).toBe(1);
    });

    it('não deixa a contagem de não lidos ficar abaixo de zero', async () => {
      const { store, http } = setup();
      const target = myAnnouncement({ category: 'general' });
      await loaded(store, http, [target], { unread: 0, unreadByCategory: NOTHING_UNREAD });

      const marking = store.markRead(target);
      http
        .expectOne(`/api/announcements/${target.id}/read`)
        .flush(null, { status: 204, statusText: 'No Content' });
      await marking;

      expect(store.unread()).toBe(0);
      expect(store.unreadByCategory()).toEqual(NOTHING_UNREAD);
    });
  });

  describe('markAll', () => {
    it('marca tudo como lido, avisa quantos foram e busca o mural de novo', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const marking = store.markAll();
      expect(store.markingAll()).toBe(true);
      const request = http.expectOne((r) => r.url === READ_ALL);
      expect(request.request.method).toBe('POST');
      expect(params(request)).toBe('');
      request.flush({ marked: 4 });
      await flushPromises();
      const fresh = [myAnnouncement({ read: true, readAt: '2026-11-22T15:00:00Z' })];
      mineRequest(http).flush(myAnnouncementPage(fresh));
      await marking;

      expect(store.markingAll()).toBe(false);
      expect(store.notice()).toBe('4 avisos marcados como lidos');
      expect(store.items()).toEqual(fresh);
      expect(store.unread()).toBe(0);
    });

    it('marca só a categoria que está na tela', async () => {
      const { store, http } = setup();
      await loaded(store, http);
      const filtering = store.setCategory('important');
      mineRequest(http).flush(myAnnouncementPage([myAnnouncement()], { unread: 4 }));
      await filtering;

      const marking = store.markAll();
      const request = http.expectOne((r) => r.url === READ_ALL);
      request.flush({ marked: 1 });
      await flushPromises();
      const refetch = mineRequest(http);
      refetch.flush(myAnnouncementPage([]));
      await marking;

      expect(params(request)).toBe('category=important');
      expect(params(refetch)).toBe('page=1&pageSize=10&category=important');
      expect(store.notice()).toBe('1 aviso marcado como lido');
    });

    it('diz que não havia nada para marcar quando a API não marcou nenhum', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const marking = store.markAll();
      http.expectOne((r) => r.url === READ_ALL).flush({ marked: 0 });
      await flushPromises();
      mineRequest(http).flush(myAnnouncementPage([]));
      await marking;

      expect(store.notice()).toBe('Nenhum aviso novo para marcar');
    });

    it('avisa e não busca o mural quando a API recusa', async () => {
      const { store, http } = setup();
      const items = [myAnnouncement()];
      await loaded(store, http, items);

      const marking = store.markAll();
      http.expectOne((r) => r.url === READ_ALL).flush(...fail(500, 'erro'));
      await marking;

      expect(store.actionError()).toBe(
        'Não foi possível marcar os avisos como lidos. Tente de novo.',
      );
      expect(store.notice()).toBeNull();
      expect(store.markingAll()).toBe(false);
      expect(store.items()).toEqual(items);
    });
  });

  describe('avisos da tela', () => {
    it('uma ação nova apaga o aviso e o erro da anterior', async () => {
      const { store, http } = setup();
      const target = myAnnouncement();
      await loaded(store, http, [target, myAnnouncement()]);
      const failing = store.markRead(target);
      http.expectOne(`/api/announcements/${target.id}/read`).flush(...fail(500, 'erro'));
      await failing;
      expect(store.actionError()).not.toBeNull();

      const refreshing = store.refresh();
      expect(store.actionError()).toBeNull();
      mineRequest(http).flush(myAnnouncementPage([]));
      await refreshing;
    });

    it('dismissNotice apaga o aviso de sucesso', async () => {
      const { store, http } = setup();
      await loaded(store, http);
      const marking = store.markAll();
      http.expectOne((r) => r.url === READ_ALL).flush({ marked: 2 });
      await flushPromises();
      mineRequest(http).flush(myAnnouncementPage([]));
      await marking;
      expect(store.notice()).not.toBeNull();

      store.dismissNotice();

      expect(store.notice()).toBeNull();
    });
  });
});

const FRESH = { general: 3, maintenance: 1, important: 1 };

function update(overrides: Partial<MuralUpdate> = {}): MuralUpdate {
  return {
    kind: 'new',
    resync: false,
    counters: { unread: 5, unreadByCategory: FRESH },
    ...overrides,
  };
}

describe('MuralStore ao vivo', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('começa sem nada pendente', () => {
    const { store } = setup();

    expect(store.pending()).toBeNull();
  });

  describe('aviso novo do servidor', () => {
    it('mostra que há avisos novos e atualiza as contagens e os filtros sem mexer na lista', async () => {
      const { store, http, live } = setup();
      const items = [myAnnouncement(), myAnnouncement()];
      await loaded(store, http, items);

      live.push(update());

      expect(store.pending()).toBe('new');
      expect(store.unread()).toBe(5);
      expect(store.unreadByCategory()).toEqual(FRESH);
      expect(store.chips().map((chip) => chip.unread)).toEqual([5, 3, 1, 1]);
      expect(store.items()).toEqual(items);
      expect(store.total()).toBe(2);
    });

    it('uma mudança em avisos que já existiam pede para atualizar, com outro texto', async () => {
      const { store, http, live } = setup();
      await loaded(store, http);

      live.push(update({ kind: 'changed' }));

      expect(store.pending()).toBe('changed');
    });

    it('o aviso novo não é desfeito por uma mudança que chega depois', async () => {
      const { store, http, live } = setup();
      await loaded(store, http);

      live.push(update({ kind: 'new' }));
      live.push(update({ kind: 'changed' }));

      expect(store.pending()).toBe('new');
    });

    it('uma mudança que vira aviso novo promove o pedido', async () => {
      const { store, http, live } = setup();
      await loaded(store, http);

      live.push(update({ kind: 'changed' }));
      live.push(update({ kind: 'new' }));

      expect(store.pending()).toBe('new');
    });

    it('sem contagens novas só mostra o pedido, e os números ficam como estavam', async () => {
      const { store, http, live } = setup();
      await loaded(store, http);

      live.push(update({ counters: null }));

      expect(store.pending()).toBe('new');
      expect(store.unread()).toBe(4);
      expect(store.unreadByCategory()).toEqual(UNREAD);
    });

    it('com o mural ainda carregando não faz nada: a busca em andamento já traz o que há', () => {
      const { store, live } = setup();

      live.push(update());

      expect(store.pending()).toBeNull();
      expect(store.unread()).toBe(0);
    });

    it('com o mural em erro também não faz nada', async () => {
      const { store, http, live } = setup();
      const loading = store.load();
      mineRequest(http).flush('erro', { status: 500, statusText: 'Erro' });
      await loading;

      live.push(update());

      expect(store.status()).toBe('error');
      expect(store.pending()).toBeNull();
    });
  });

  describe('reconexão', () => {
    it('se as contagens são as mesmas, nada mudou e a faixa não aparece', async () => {
      const { store, http, live } = setup();
      await loaded(store, http);

      live.push(
        update({
          kind: 'changed',
          resync: true,
          counters: { unread: 4, unreadByCategory: UNREAD },
        }),
      );

      expect(store.pending()).toBeNull();
    });

    it('se as contagens mudaram, algo aconteceu no meio e a faixa aparece', async () => {
      const { store, http, live } = setup();
      await loaded(store, http);

      live.push(update({ kind: 'changed', resync: true }));

      expect(store.pending()).toBe('changed');
      expect(store.unread()).toBe(5);
    });

    it('sem contagens para comparar, avisa por precaução', async () => {
      const { store, http, live } = setup();
      await loaded(store, http);

      live.push(update({ kind: 'changed', resync: true, counters: null }));

      expect(store.pending()).toBe('changed');
    });
  });

  describe('atualizar', () => {
    it('depois da busca o pedido some e a lista tem o que veio', async () => {
      const { store, http, live } = setup();
      await loaded(store, http);
      live.push(update());
      const fresh = [myAnnouncement(), myAnnouncement(), myAnnouncement()];

      const refreshing = store.refresh();
      mineRequest(http).flush(myAnnouncementPage(fresh, { unread: 5, unreadByCategory: FRESH }));
      await refreshing;

      expect(store.pending()).toBeNull();
      expect(store.items()).toEqual(fresh);
    });

    it('trocar o filtro também apaga o pedido, porque a lista foi buscada de novo', async () => {
      const { store, http, live } = setup();
      await loaded(store, http);
      live.push(update());

      const changing = store.setCategory('important');
      mineRequest(http).flush(
        myAnnouncementPage([myAnnouncement()], { unread: 5, unreadByCategory: FRESH }),
      );
      await changing;

      expect(store.pending()).toBeNull();
    });

    it('tentar de novo depois de um erro apaga o pedido', async () => {
      const { store, http, live } = setup();
      await loaded(store, http);
      live.push(update());

      const loading = store.load();
      mineRequest(http).flush(myAnnouncementPage([myAnnouncement()]));
      await loading;

      expect(store.pending()).toBeNull();
    });

    it('um aviso que chega durante a busca vale: o pedido volta', async () => {
      const { store, http, live } = setup();
      await loaded(store, http);

      const refreshing = store.refresh();
      live.push(update());
      mineRequest(http).flush(myAnnouncementPage([myAnnouncement()]));
      await refreshing;

      expect(store.pending()).toBe('new');
    });
  });

  describe('a contagem do menu', () => {
    it('recebe a contagem do mural assim que ele fica pronto', async () => {
      const { store, http, live } = setup();

      await loaded(store, http);
      TestBed.tick();

      expect(live.setUnread).toHaveBeenLastCalledWith(4);
    });

    it('não recebe nada enquanto o mural ainda carrega, para não zerar a bolinha', () => {
      const { live } = setup();
      TestBed.tick();

      expect(live.setUnread).not.toHaveBeenCalled();
    });

    it('acompanha o aviso lido', async () => {
      const { store, http, live } = setup();
      const items = [myAnnouncement(), myAnnouncement()];
      await loaded(store, http, items);
      TestBed.tick();

      const marking = store.markRead(items[0]);
      http.expectOne((request) => request.url.endsWith('/read')).flush(null);
      await marking;
      TestBed.tick();

      expect(live.setUnread).toHaveBeenLastCalledWith(3);
    });

    it('acompanha as contagens que chegam do servidor', async () => {
      const { store, http, live } = setup();
      await loaded(store, http);
      TestBed.tick();

      live.push(update());
      TestBed.tick();

      expect(live.setUnread).toHaveBeenLastCalledWith(5);
    });
  });
});
