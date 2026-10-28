import {
  DOCUMENT,
  DestroyRef,
  EnvironmentProviders,
  Injectable,
  InjectionToken,
  Injector,
  Signal,
  effect,
  inject,
  provideAppInitializer,
  signal,
  untracked,
} from '@angular/core';
import { Router } from '@angular/router';
import { ActivityLog } from './activity-log';
import { idleState } from './inactivity';
import { SessionStore } from './session.store';

export interface IdleWarning {
  close(): void;
}

/** Abre o aviso "Você ainda está aí?". `keep` é chamado quando a pessoa escolhe continuar. */
export type OpenIdleWarning = (
  secondsLeft: Signal<number>,
  keep: () => void,
) => Promise<IdleWarning>;

/**
 * O diálogo (e o Angular Material Dialog) só é baixado quando o aviso aparece, o que quase
 * nunca acontece: assim a inatividade não pesa no carregamento inicial.
 */
export const IDLE_WARNING = new InjectionToken<OpenIdleWarning>('IDLE_WARNING', {
  providedIn: 'root',
  factory: (): OpenIdleWarning => {
    const injector = inject(Injector);
    return async (secondsLeft, keep): Promise<IdleWarning> => {
      const { openIdleWarning } = await import('../layout/idle-warning.dialog');
      return openIdleWarning(injector, secondsLeft, keep);
    };
  },
});

const ACTIVITY_EVENTS = ['pointerdown', 'pointermove', 'keydown', 'wheel', 'touchstart'] as const;
const TICK_MS = 1_000;

/**
 * Encerra a sessão depois de 60 minutos sem atividade, avisando 1 minuto antes.
 *
 * Só vigia enquanto há sessão. A cada segundo compara o relógio com o registro da última
 * atividade (ActivityLog): como o registro é compartilhado entre as abas, a aba que ficou
 * parada acompanha o uso das outras. Com o aviso aberto, só o botão "Continuar conectado"
 * mantém a sessão: um esbarrão no mouse não deve esconder o aviso de quem saiu da frente da tela.
 */
@Injectable({ providedIn: 'root' })
export class InactivityMonitor {
  private readonly session = inject(SessionStore);
  private readonly router = inject(Router);
  private readonly log = inject(ActivityLog);
  private readonly document = inject(DOCUMENT);
  private readonly openWarning = inject(IDLE_WARNING);

  private readonly secondsLeft = signal(0);
  private timer: ReturnType<typeof setInterval> | null = null;
  private warningOpen = false;
  private warning: IdleWarning | null = null;
  private warningId = 0;
  private lastHandled = 0;

  private readonly onActivity = (): void => this.handleActivity();

  constructor() {
    effect(() => {
      const authenticated = this.session.status() === 'authenticated';
      untracked(() => (authenticated ? this.watch() : this.unwatch()));
    });
    inject(DestroyRef).onDestroy(() => this.unwatch());
  }

  private watch(): void {
    if (this.timer !== null) {
      return;
    }
    this.log.touch(Date.now(), true);
    for (const type of ACTIVITY_EVENTS) {
      this.document.addEventListener(type, this.onActivity, { capture: true, passive: true });
    }
    this.timer = setInterval(() => void this.tick(), TICK_MS);
  }

  private unwatch(): void {
    if (this.timer === null) {
      return;
    }
    clearInterval(this.timer);
    this.timer = null;
    for (const type of ACTIVITY_EVENTS) {
      this.document.removeEventListener(type, this.onActivity, { capture: true });
    }
    this.closeWarning();
  }

  private handleActivity(): void {
    const now = Date.now();
    // Movimentos do mouse chegam aos montes; um registro por segundo basta.
    if (this.warningOpen || now - this.lastHandled < TICK_MS) {
      return;
    }
    this.lastHandled = now;
    // Quem volta ao computador depois do limite já perdeu a sessão: o próximo tick a encerra.
    if (idleState(now, this.log.lastActivity()).phase !== 'expired') {
      this.log.touch(now);
    }
  }

  private async tick(): Promise<void> {
    const state = idleState(Date.now(), this.log.lastActivity());
    if (state.phase === 'active') {
      this.closeWarning();
    } else if (state.phase === 'warning') {
      this.secondsLeft.set(state.secondsLeft);
      this.showWarning();
    } else {
      await this.expire();
    }
  }

  private showWarning(): void {
    if (this.warningOpen) {
      return;
    }
    this.warningOpen = true;
    const id = ++this.warningId;
    void this.openWarning(this.secondsLeft, () => this.keepAlive()).then((handle) => {
      if (this.warningOpen && id === this.warningId) {
        this.warning = handle;
      } else {
        handle.close();
      }
    });
  }

  private closeWarning(): void {
    this.warningOpen = false;
    this.warning?.close();
    this.warning = null;
  }

  private keepAlive(): void {
    this.log.touch(Date.now(), true);
    this.closeWarning();
  }

  private async expire(): Promise<void> {
    this.unwatch();
    await this.session.signOut();
    await this.router.navigate(['/entrar'], { queryParams: { motivo: 'inatividade' } });
  }
}

/** Liga a vigilância de inatividade ao abrir o app, no mesmo molde da restauração da sessão. */
export function provideInactivityLogout(): EnvironmentProviders {
  return provideAppInitializer(() => {
    inject(InactivityMonitor);
  });
}
