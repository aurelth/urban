import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { Signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { Router, provideRouter } from '@angular/router';
import { jest } from '@jest/globals';
import { flushPromises } from '../../testing/async';
import { signInAs } from '../../testing/auth.fixtures';
import {
  IDLE_WARNING,
  IdleWarning,
  InactivityMonitor,
  OpenIdleWarning,
} from './inactivity.monitor';
import { SessionStore } from './session.store';

const MINUTE = 60_000;

interface Harness {
  session: InstanceType<typeof SessionStore>;
  http: HttpTestingController;
  navigate: jest.SpiedFunction<Router['navigate']>;
  open: jest.MockedFunction<OpenIdleWarning>;
  closeWarning: jest.Mock<() => void>;
  secondsLeft: () => Signal<number>;
  keep: () => () => void;
}

/** Relogio falso so para Date e setInterval: o resto (promessas, setTimeout) segue real. */
function setup(): Harness {
  jest.useFakeTimers({
    doNotFake: [
      'setTimeout',
      'clearTimeout',
      'setImmediate',
      'clearImmediate',
      'nextTick',
      'queueMicrotask',
    ],
  });
  localStorage.clear();
  const closeWarning = jest.fn<() => void>();
  let seconds!: Signal<number>;
  let onKeep!: () => void;
  const open = jest.fn<OpenIdleWarning>(
    (secondsLeft: Signal<number>, keep: () => void): Promise<IdleWarning> => {
      seconds = secondsLeft;
      onKeep = keep;
      return Promise.resolve({ close: closeWarning });
    },
  );
  TestBed.configureTestingModule({
    providers: [
      provideRouter([]),
      provideHttpClient(),
      provideHttpClientTesting(),
      { provide: IDLE_WARNING, useValue: open },
    ],
  });
  const navigate = jest.spyOn(TestBed.inject(Router), 'navigate').mockResolvedValue(true);
  TestBed.inject(InactivityMonitor);
  pending = TestBed.inject(HttpTestingController);
  return {
    session: TestBed.inject(SessionStore),
    http: pending,
    navigate,
    open,
    closeWarning,
    secondsLeft: () => seconds,
    keep: () => onKeep,
  };
}

async function signIn(harness: Harness): Promise<void> {
  await signInAs(harness.session, harness.http);
  TestBed.tick();
}

function advance(ms: number): void {
  jest.advanceTimersByTime(ms);
}

let pending: HttpTestingController | undefined;

describe('InactivityMonitor', () => {
  afterEach(() => {
    jest.useRealTimers();
    jest.restoreAllMocks();
    pending?.match(() => true);
  });

  it('avisa 1 minuto antes de sair, com a contagem regressiva', async () => {
    const harness = setup();
    await signIn(harness);

    advance(59 * MINUTE - 1_000);
    expect(harness.open).not.toHaveBeenCalled();

    advance(1_000);
    expect(harness.open).toHaveBeenCalledTimes(1);
    expect(harness.secondsLeft()()).toBe(60);

    advance(10_000);
    expect(harness.secondsLeft()()).toBe(50);
    expect(harness.open).toHaveBeenCalledTimes(1);
  });

  it('sai da conta e volta para a entrada, avisando o motivo, ao completar 60 minutos', async () => {
    const harness = setup();
    await signIn(harness);

    advance(60 * MINUTE);
    harness.http
      .expectOne('/api/auth/logout')
      .flush(null, { status: 204, statusText: 'No Content' });
    await flushPromises();

    expect(harness.session.status()).toBe('anonymous');
    expect(harness.navigate).toHaveBeenCalledWith(['/entrar'], {
      queryParams: { motivo: 'inatividade' },
    });
    expect(harness.closeWarning).toHaveBeenCalledTimes(1);
  });

  it('sai tambem quando a API de saida nao responde', async () => {
    const harness = setup();
    await signIn(harness);

    advance(60 * MINUTE);
    harness.http.expectOne('/api/auth/logout').error(new ProgressEvent('error'));
    await flushPromises();

    expect(harness.session.status()).toBe('anonymous');
    expect(harness.navigate).toHaveBeenCalledTimes(1);
  });

  it('atividade do usuario na tela adia o aviso e a saida', async () => {
    const harness = setup();
    await signIn(harness);

    advance(30 * MINUTE);
    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'a' }));
    advance(58 * MINUTE);

    expect(harness.open).not.toHaveBeenCalled();
    harness.http.expectNone('/api/auth/logout');

    advance(2 * MINUTE);
    expect(harness.open).toHaveBeenCalledTimes(1);
  });

  it.each(['pointerdown', 'pointermove', 'wheel', 'touchstart'])(
    'o evento %s tambem conta como atividade',
    async (type) => {
      const harness = setup();
      await signIn(harness);

      advance(40 * MINUTE);
      document.dispatchEvent(new Event(type));
      advance(40 * MINUTE);

      expect(harness.open).not.toHaveBeenCalled();
    },
  );

  it('atividade em outra aba do mesmo navegador adia o aviso e a saida', async () => {
    const harness = setup();
    await signIn(harness);

    advance(30 * MINUTE);
    localStorage.setItem('urban.last-activity', String(Date.now()));
    advance(58 * MINUTE);

    expect(harness.open).not.toHaveBeenCalled();
    harness.http.expectNone('/api/auth/logout');
  });

  it('fecha o aviso quando outra aba teve atividade', async () => {
    const harness = setup();
    await signIn(harness);
    advance(59 * MINUTE);
    await flushPromises();

    localStorage.setItem('urban.last-activity', String(Date.now()));
    advance(1_000);

    expect(harness.closeWarning).toHaveBeenCalledTimes(1);
  });

  it('com o aviso aberto, so o botao de continuar mantem a sessao', async () => {
    const harness = setup();
    await signIn(harness);
    advance(59 * MINUTE);
    await flushPromises();

    document.dispatchEvent(new KeyboardEvent('keydown', { key: 'a' }));
    advance(30_000);
    expect(harness.secondsLeft()()).toBe(30);

    harness.keep()();
    advance(58 * MINUTE);

    expect(harness.open).toHaveBeenCalledTimes(1);
    harness.http.expectNone('/api/auth/logout');

    advance(2 * MINUTE);
    expect(harness.open).toHaveBeenCalledTimes(2);
  });

  it('quem volta ao computador depois do limite nao salva a sessao mexendo o mouse', async () => {
    const harness = setup();
    await signIn(harness);

    // O computador dormiu: o relogio andou 2 horas sem nenhum tick.
    jest.setSystemTime(Date.now() + 2 * 60 * MINUTE);
    document.dispatchEvent(new Event('pointermove'));
    advance(1_000);
    harness.http
      .expectOne('/api/auth/logout')
      .flush(null, { status: 204, statusText: 'No Content' });
    await flushPromises();

    expect(harness.session.status()).toBe('anonymous');
  });

  it('nao vigia quem nao esta logado', async () => {
    const harness = setup();
    TestBed.tick();

    advance(3 * 60 * MINUTE);

    expect(harness.open).not.toHaveBeenCalled();
    harness.http.expectNone('/api/auth/logout');
    expect(harness.navigate).not.toHaveBeenCalled();
  });

  it('para de vigiar quando a pessoa sai da conta', async () => {
    const harness = setup();
    await signIn(harness);

    const signingOut = harness.session.signOut();
    harness.http
      .expectOne('/api/auth/logout')
      .flush(null, { status: 204, statusText: 'No Content' });
    await signingOut;
    TestBed.tick();
    advance(3 * 60 * MINUTE);

    expect(harness.open).not.toHaveBeenCalled();
    harness.http.expectNone('/api/auth/logout');
  });

  it('para de vigiar quando o app e destruido, sem deixar ouvintes para tras', async () => {
    const harness = setup();
    await signIn(harness);

    TestBed.resetTestingModule();
    advance(3 * 60 * MINUTE);

    expect(harness.open).not.toHaveBeenCalled();
  });

  it('volta a vigiar quando a pessoa entra de novo, contando do zero', async () => {
    const harness = setup();
    await signIn(harness);
    const signingOut = harness.session.signOut();
    harness.http
      .expectOne('/api/auth/logout')
      .flush(null, { status: 204, statusText: 'No Content' });
    await signingOut;
    TestBed.tick();
    advance(2 * 60 * MINUTE);

    await signIn(harness);
    advance(59 * MINUTE - 1_000);
    expect(harness.open).not.toHaveBeenCalled();

    advance(1_000);
    expect(harness.open).toHaveBeenCalledTimes(1);
  });
});
