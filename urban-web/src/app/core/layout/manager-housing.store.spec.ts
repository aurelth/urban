import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { flushPromises } from '../../testing/async';
import { ManagerHousingStore } from './manager-housing.store';

const ROUTE = '/api/finance/me/access';

describe('ManagerHousingStore', () => {
  let store: InstanceType<typeof ManagerHousingStore>;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    store = TestBed.inject(ManagerHousingStore);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  it('começa sem dizer que o síndico mora no condomínio', () => {
    expect(store.livesHere()).toBe(false);
  });

  it('mora no condomínio quando o acesso é de unidade', async () => {
    const loading = store.load('u-1');
    http.expectOne(ROUTE).flush({ access: 'unit' });
    await loading;

    expect(store.livesHere()).toBe(true);
  });

  it.each(['dependent', 'none'])('não conta como morador quando o acesso é %s', async (access) => {
    const loading = store.load('u-1');
    http.expectOne(ROUTE).flush({ access });
    await loading;

    expect(store.livesHere()).toBe(false);
  });

  it('só aceita a resposta exata "unit": qualquer outra coisa não mostra o item', async () => {
    const loading = store.load('u-1');
    http.expectOne(ROUTE).flush({ access: 'UNIT' });
    await loading;

    expect(store.livesHere()).toBe(false);
  });

  it('falha em silêncio: sem a resposta o síndico só fica sem o item', async () => {
    const loading = store.load('u-1');
    http.expectOne(ROUTE).flush({ code: 'x' }, { status: 503, statusText: 'Service Unavailable' });

    await expect(loading).resolves.toBeUndefined();
    expect(store.livesHere()).toBe(false);
  });

  it('não pergunta de novo para a mesma pessoa', async () => {
    const first = store.load('u-1');
    http.expectOne(ROUTE).flush({ access: 'unit' });
    await first;

    await store.load('u-1');

    http.expectNone(ROUTE);
    expect(store.livesHere()).toBe(true);
  });

  it('pergunta de novo para a mesma pessoa depois de uma falha', async () => {
    const first = store.load('u-1');
    http.expectOne(ROUTE).flush({}, { status: 503, statusText: 'Service Unavailable' });
    await first;

    const second = store.load('u-1');
    http.expectOne(ROUTE).flush({ access: 'unit' });
    await second;

    expect(store.livesHere()).toBe(true);
  });

  it('descarta a resposta de quem já não é a pessoa da sessão', async () => {
    const first = store.load('u-1');
    const second = store.load('u-2');
    const [old, current] = http.match(ROUTE);
    old.flush({ access: 'unit' });
    await flushPromises();
    current.flush({ access: 'none' });
    await Promise.all([first, second]);

    expect(store.livesHere()).toBe(false);
  });

  it('esquece tudo ao sair, e o próximo a entrar é perguntado do zero', async () => {
    const first = store.load('u-1');
    http.expectOne(ROUTE).flush({ access: 'unit' });
    await first;

    store.reset();
    expect(store.livesHere()).toBe(false);

    const again = store.load('u-1');
    http.expectOne(ROUTE).flush({ access: 'none' });
    await again;
    expect(store.livesHere()).toBe(false);
  });

  it('uma resposta que chega depois de sair não devolve o item', async () => {
    const loading = store.load('u-1');
    store.reset();
    http.expectOne(ROUTE).flush({ access: 'unit' });
    await loading;

    expect(store.livesHere()).toBe(false);
  });

  describe('refresh', () => {
    it('pergunta de novo para a mesma pessoa e passa a dizer que ela mora', async () => {
      const first = store.load('u-1');
      http.expectOne(ROUTE).flush({ access: 'none' });
      await first;
      expect(store.livesHere()).toBe(false);

      const refreshing = store.refresh('u-1');
      http.expectOne(ROUTE).flush({ access: 'unit' });
      await refreshing;

      expect(store.livesHere()).toBe(true);
    });

    it('depois de sair da unidade, o item some', async () => {
      const first = store.load('u-1');
      http.expectOne(ROUTE).flush({ access: 'unit' });
      await first;

      const refreshing = store.refresh('u-1');
      http.expectOne(ROUTE).flush({ access: 'none' });
      await refreshing;

      expect(store.livesHere()).toBe(false);
    });

    it('mantém a resposta antiga enquanto espera, sem piscar o item', async () => {
      const first = store.load('u-1');
      http.expectOne(ROUTE).flush({ access: 'unit' });
      await first;

      const refreshing = store.refresh('u-1');
      expect(store.livesHere()).toBe(true);
      http.expectOne(ROUTE).flush({ access: 'unit' });
      await refreshing;
    });

    it('falha em silêncio e deixa a resposta que já tinha', async () => {
      const first = store.load('u-1');
      http.expectOne(ROUTE).flush({ access: 'unit' });
      await first;

      const refreshing = store.refresh('u-1');
      http.expectOne(ROUTE).flush({}, { status: 503, statusText: 'Service Unavailable' });

      await expect(refreshing).resolves.toBeUndefined();
      expect(store.livesHere()).toBe(true);
    });

    it('a resposta que chega depois de a sessão trocar de dono é descartada', async () => {
      const first = store.load('u-1');
      http.expectOne(ROUTE).flush({ access: 'none' });
      await first;

      const refreshing = store.refresh('u-1');
      store.reset();
      http.expectOne(ROUTE).flush({ access: 'unit' });
      await refreshing;

      expect(store.livesHere()).toBe(false);
    });
  });
});
