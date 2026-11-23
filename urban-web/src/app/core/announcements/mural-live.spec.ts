import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { HUB_LINK } from '../realtime/hub-link';
import { SessionStore } from '../auth/session.store';
import { FakeHubLink, fakeHubLinks } from '../../testing/hub-link.fake';
import { flushPromises } from '../../testing/async';
import { signInAs, tokenResponse } from '../../testing/auth.fixtures';
import { MURAL_LIVE_RETRY_MS, MuralLive, MuralUpdate } from './mural-live';
import { myAnnouncementPage } from '../../testing/announcements.fixtures';

const MINE = '/api/announcements/mine';
const ID = '0199cccc-0000-7000-8000-000000000001';
const BY_CATEGORY = { general: 2, maintenance: 1, important: 0 };

interface Harness {
  live: MuralLive;
  http: HttpTestingController;
  session: InstanceType<typeof SessionStore>;
  links: FakeHubLink[];
  updates: MuralUpdate[];
}

async function setup(failFirstStarts = 0): Promise<Harness> {
  const hubs = fakeHubLinks(failFirstStarts);
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: HUB_LINK, useValue: hubs.factory },
      { provide: MURAL_LIVE_RETRY_MS, useValue: 0 },
    ],
  });
  const http = TestBed.inject(HttpTestingController);
  const session = TestBed.inject(SessionStore);
  await signInAs(session, http);
  const live = TestBed.inject(MuralLive);
  const updates: MuralUpdate[] = [];
  live.updates.subscribe((update) => updates.push(update));
  return { live, http, session, links: hubs.links, updates };
}

/** A busca leve das contagens: a primeira página, de um aviso só, sem filtro. */
function countersRequest(http: HttpTestingController): TestRequest {
  return http.expectOne(
    (request) =>
      request.url === MINE &&
      request.method === 'GET' &&
      request.params.get('page') === '1' &&
      request.params.get('pageSize') === '1' &&
      !request.params.has('unreadOnly') &&
      !request.params.has('category'),
  );
}

function answer(request: TestRequest, unread: number): void {
  request.flush(myAnnouncementPage([], { unread, unreadByCategory: BY_CATEGORY }));
}

/** Começa e responde às contagens do começo. */
async function started(unread = 3): Promise<Harness> {
  const harness = await setup();
  harness.live.start();
  await flushPromises();
  answer(countersRequest(harness.http), unread);
  await flushPromises();
  return harness;
}

describe('MuralLive', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('começa sem avisos não lidos e sem conexão', async () => {
    const { live, links } = await setup();

    expect(live.unread()).toBe(0);
    expect(links).toHaveLength(0);
  });

  describe('start', () => {
    it('abre uma conexão com o hub dos comunicados, com o token da sessão', async () => {
      const { live, links, http } = await setup();

      live.start();
      await flushPromises();

      expect(links).toHaveLength(1);
      expect(links[0].path).toBe('/hubs/announcements');
      expect(links[0].accessToken()).toBe('access-1');
      expect(links[0].starts).toBe(1);
      countersRequest(http).flush(myAnnouncementPage([]));
    });

    it('busca as contagens uma vez e a bolinha do menu as mostra', async () => {
      const { live } = await started(5);

      expect(live.unread()).toBe(5);
    });

    it('chamar de novo com a conexão aberta não abre outra', async () => {
      const { live, links } = await started();

      live.start();
      await flushPromises();

      expect(links).toHaveLength(1);
      expect(links[0].starts).toBe(1);
    });

    it('não publica nada só por começar: a faixa do mural é para o que muda depois', async () => {
      const { updates } = await started();

      expect(updates).toEqual([]);
    });
  });

  describe('avisos do servidor', () => {
    it('um aviso publicado busca as contagens e chega como novo', async () => {
      const { live, links, http, updates } = await started(3);

      links[0].emit('announcementChanged', { kind: 'published', id: ID });
      await flushPromises();
      answer(countersRequest(http), 4);
      await flushPromises();

      expect(live.unread()).toBe(4);
      expect(updates).toEqual([
        {
          kind: 'new',
          resync: false,
          counters: { unread: 4, unreadByCategory: BY_CATEGORY },
        },
      ]);
    });

    it.each(['updated', 'removed'])('um aviso %s chega como mudança', async (kind) => {
      const { links, http, updates } = await started(3);

      links[0].emit('announcementChanged', { kind, id: ID });
      await flushPromises();
      answer(countersRequest(http), 2);
      await flushPromises();

      expect(updates).toHaveLength(1);
      expect(updates[0].kind).toBe('changed');
      expect(updates[0].counters?.unread).toBe(2);
    });

    it('descarta o que não tem o formato do aviso, sem buscar nada', async () => {
      const { links, updates } = await started();

      links[0].emit('announcementChanged', { kind: 'deleted', id: ID });
      links[0].emit('announcementChanged', 'published');
      await flushPromises();

      expect(updates).toEqual([]);
    });

    it('vários avisos seguidos viram uma busca em andamento e uma de acompanhamento, e a mudança mais forte vence', async () => {
      const { live, links, http, updates } = await started(3);

      links[0].emit('announcementChanged', { kind: 'updated', id: ID });
      await flushPromises();
      const first = countersRequest(http);
      links[0].emit('announcementChanged', { kind: 'published', id: ID });
      links[0].emit('announcementChanged', { kind: 'removed', id: ID });
      links[0].emit('announcementChanged', { kind: 'published', id: ID });
      answer(first, 3);
      await flushPromises();
      answer(countersRequest(http), 5);
      await flushPromises();

      expect(updates.map((update) => update.kind)).toEqual(['changed', 'new']);
      expect(live.unread()).toBe(5);
    });

    it('se as contagens não vêm, a mudança chega assim mesmo, sem contagens, e a bolinha fica como estava', async () => {
      const { live, links, http, updates } = await started(3);

      links[0].emit('announcementChanged', { kind: 'published', id: ID });
      await flushPromises();
      countersRequest(http).flush('erro', { status: 500, statusText: 'Erro' });
      await flushPromises();

      expect(updates).toEqual([{ kind: 'new', resync: false, counters: null }]);
      expect(live.unread()).toBe(3);
    });
  });

  describe('reconexão', () => {
    it('a conexão que volta sozinha busca as contagens e avisa que pode ter havido mudança no meio', async () => {
      const { live, links, http, updates } = await started(3);

      links[0].reconnected();
      await flushPromises();
      answer(countersRequest(http), 6);
      await flushPromises();

      expect(live.unread()).toBe(6);
      expect(updates).toEqual([
        { kind: 'changed', resync: true, counters: { unread: 6, unreadByCategory: BY_CATEGORY } },
      ]);
    });

    it('uma conexão que não abre espera, renova a sessão e tenta de novo', async () => {
      const { live, links, http, session } = await setup(1);

      live.start();
      await flushPromises();
      await flushPromises();
      http.expectOne('/api/auth/refresh').flush(tokenResponse('access-2'));
      await flushPromises();
      await flushPromises();

      expect(links).toHaveLength(1);
      expect(links[0].starts).toBe(2);
      expect(session.accessToken()).toBe('access-2');
      expect(links[0].accessToken()).toBe('access-2');
      countersRequest(http).flush(myAnnouncementPage([]));
    });

    it('uma conexão que acaba sozinha também tenta de novo e, ao voltar, busca as contagens como numa reconexão', async () => {
      const { live, links, http, updates } = await started(3);

      links[0].closed();
      await flushPromises();
      await flushPromises();
      http.expectOne('/api/auth/refresh').flush(tokenResponse('access-2'));
      await flushPromises();
      await flushPromises();
      answer(countersRequest(http), 7);
      await flushPromises();

      expect(links[0].starts).toBe(2);
      expect(live.unread()).toBe(7);
      expect(updates).toEqual([
        { kind: 'changed', resync: true, counters: { unread: 7, unreadByCategory: BY_CATEGORY } },
      ]);
    });

    it('se a sessão não se renova, não tenta abrir de novo agora', async () => {
      const { links, http } = await started();

      links[0].closed();
      await flushPromises();
      await flushPromises();
      http
        .expectOne('/api/auth/refresh')
        .flush(
          { code: 'identity.session.unauthenticated' },
          { status: 401, statusText: 'Não autorizado' },
        );
      await flushPromises();
      await flushPromises();

      expect(links[0].starts).toBe(1);
    });
  });

  describe('stop', () => {
    it('fecha a conexão, zera a contagem e deixa de ouvir', async () => {
      const { live, links, updates } = await started(4);

      live.stop();
      await flushPromises();
      links[0].emit('announcementChanged', { kind: 'published', id: ID });
      await flushPromises();

      expect(links[0].stops).toBe(1);
      expect(live.unread()).toBe(0);
      expect(updates).toEqual([]);
    });

    it('depois de parar, a conexão que acaba não tenta abrir de novo', async () => {
      const { live, links } = await started();

      live.stop();
      await flushPromises();
      links[0].closed();
      await flushPromises();
      await flushPromises();

      expect(links[0].starts).toBe(1);
    });

    it('a resposta das contagens que chega depois de parar é descartada', async () => {
      const { live, http } = await setup();
      live.start();
      await flushPromises();
      const pending = countersRequest(http);

      live.stop();
      answer(pending, 9);
      await flushPromises();

      expect(live.unread()).toBe(0);
    });

    it('começar de novo depois de parar abre uma conexão nova', async () => {
      const { live, links, http } = await started();

      live.stop();
      live.start();
      await flushPromises();
      answer(countersRequest(http), 1);
      await flushPromises();

      expect(links).toHaveLength(2);
      expect(live.unread()).toBe(1);
    });

    it('parar sem ter começado não faz nada', async () => {
      const { live, links } = await setup();

      live.stop();

      expect(links).toHaveLength(0);
    });
  });

  describe('setUnread', () => {
    it('o mural aberto põe a contagem mais fresca, e a bolinha a segue', async () => {
      const { live } = await started(5);

      live.setUnread(4);

      expect(live.unread()).toBe(4);
    });
  });
});
