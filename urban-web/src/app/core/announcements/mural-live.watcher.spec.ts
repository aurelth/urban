import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { HUB_LINK } from '../realtime/hub-link';
import { SessionStore } from '../auth/session.store';
import { fakeHubLinks } from '../../testing/hub-link.fake';
import { flushPromises } from '../../testing/async';
import { MANAGER, REGISTERED_MANAGER, signInAs } from '../../testing/auth.fixtures';
import { myAnnouncementPage } from '../../testing/announcements.fixtures';
import { CurrentUser } from '../auth/auth.models';
import { MuralLive } from './mural-live';
import { MuralLiveWatcher } from './mural-live.watcher';

function setup(): {
  session: InstanceType<typeof SessionStore>;
  http: HttpTestingController;
  links: ReturnType<typeof fakeHubLinks>['links'];
  live: MuralLive;
} {
  const hubs = fakeHubLinks();
  TestBed.configureTestingModule({
    providers: [
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: HUB_LINK, useValue: hubs.factory },
    ],
  });
  TestBed.inject(MuralLiveWatcher);
  return {
    session: TestBed.inject(SessionStore),
    http: TestBed.inject(HttpTestingController),
    links: hubs.links,
    live: TestBed.inject(MuralLive),
  };
}

const RESIDENT: CurrentUser = { ...REGISTERED_MANAGER, role: 'Resident' };
const SUPER_ADMIN: CurrentUser = { ...MANAGER, role: 'SuperAdmin' };

async function signedInAs(user: CurrentUser): Promise<ReturnType<typeof setup>> {
  const harness = setup();
  await signInAs(harness.session, harness.http, user);
  TestBed.tick();
  await flushPromises();
  return harness;
}

describe('MuralLiveWatcher', () => {
  afterEach(() => {
    TestBed.inject(HttpTestingController)
      .match((request) => request.url === '/api/announcements/mine')
      .forEach((request) => request.flush(myAnnouncementPage([])));
    TestBed.inject(HttpTestingController).verify();
  });

  it('não liga nada enquanto ninguém entrou', () => {
    const { links } = setup();
    TestBed.tick();

    expect(links).toHaveLength(0);
  });

  it('liga o mural ao vivo do morador que entrou', async () => {
    const { links } = await signedInAs(RESIDENT);

    expect(links).toHaveLength(1);
  });

  it('liga o do síndico que já cadastrou o condomínio', async () => {
    const { links } = await signedInAs(REGISTERED_MANAGER);

    expect(links).toHaveLength(1);
  });

  it('não liga para o síndico que ainda não cadastrou o condomínio', async () => {
    const { links } = await signedInAs(MANAGER);

    expect(links).toHaveLength(0);
  });

  it('não liga para o administrador da plataforma', async () => {
    const { links } = await signedInAs(SUPER_ADMIN);

    expect(links).toHaveLength(0);
  });

  it('fecha a conexão e zera a bolinha quando a pessoa sai', async () => {
    const { links, session, live, http } = await signedInAs(RESIDENT);
    http
      .expectOne((request) => request.url === '/api/announcements/mine')
      .flush(myAnnouncementPage([], { unread: 3 }));
    await flushPromises();
    expect(live.unread()).toBe(3);

    session.endSession();
    TestBed.tick();
    await flushPromises();

    expect(links[0].stops).toBe(1);
    expect(live.unread()).toBe(0);
  });
});
