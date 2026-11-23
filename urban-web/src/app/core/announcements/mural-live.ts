import { Injectable, InjectionToken, Signal, inject, signal } from '@angular/core';
import { Observable, Subject, firstValueFrom } from 'rxjs';
import { SessionStore } from '../auth/session.store';
import { HUB_LINK, HubLink } from '../realtime/hub-link';
import { ANNOUNCEMENTS_HUB_PATH, ANNOUNCEMENT_CHANGED, parseChange } from './announcement-change';
import { AnnouncementsApi } from './announcements.api';
import { UnreadByCategory } from './announcements.models';

/** Os não lidos do mural de quem está logado, como a API os conta. */
export interface MuralCounters {
  readonly unread: number;
  readonly unreadByCategory: UnreadByCategory;
}

/**
 * O que o mural precisa saber de uma mudança: se há avisos novos (`new`) ou só mudou o que já havia (`changed`), as contagens
 * de agora (nulas se não deu para buscá-las) e se veio de uma reconexão (`resync`), caso em que nem sempre mudou algo.
 */
export interface MuralUpdate {
  readonly kind: 'new' | 'changed';
  readonly resync: boolean;
  readonly counters: MuralCounters | null;
}

/** Quanto esperar antes de tentar abrir de novo uma conexão que não abriu ou que acabou. Os testes usam um valor curto. */
export const MURAL_LIVE_RETRY_MS = new InjectionToken<number>('MURAL_LIVE_RETRY_MS', {
  providedIn: 'root',
  factory: () => 30_000,
});

/** Uma ligação do mural ao vivo, do `start` ao `stop`: tudo o que ficou pendente numa ligação velha é descartado. */
interface Run {
  readonly link: HubLink;
  /** O que espera para ser publicado: a mudança mais forte e se todas vieram só de reconexão. */
  queued: { kind: MuralUpdate['kind']; resync: boolean } | null;
  fetching: boolean;
}

/**
 * O mural ao vivo: mantém a conexão com o servidor enquanto a pessoa está logada e conta os avisos não lidos para o menu. A cada
 * mudança (e a cada reconexão) busca as contagens e as publica em `updates`, que o mural aberto usa para mostrar a faixa
 * "Há avisos novos" sem mexer na lista.
 */
@Injectable({ providedIn: 'root' })
export class MuralLive {
  private readonly api = inject(AnnouncementsApi);
  private readonly session = inject(SessionStore);
  private readonly hubLink = inject(HUB_LINK);
  private readonly retryMs = inject(MURAL_LIVE_RETRY_MS);
  private readonly count = signal(0);
  private readonly changes = new Subject<MuralUpdate>();
  private run: Run | null = null;

  /** Quantos avisos não lidos há, para a bolinha do menu. */
  readonly unread: Signal<number> = this.count.asReadonly();

  readonly updates: Observable<MuralUpdate> = this.changes.asObservable();

  /** Abre a conexão e busca as contagens. Chamar de novo com a conexão aberta não faz nada. */
  start(): void {
    if (this.run) {
      return;
    }
    const link = this.hubLink(ANNOUNCEMENTS_HUB_PATH, () => this.session.accessToken() ?? '');
    const run: Run = { link, queued: null, fetching: false };
    this.run = run;

    link.on(ANNOUNCEMENT_CHANGED, (message) => {
      const change = parseChange(message);
      if (change) {
        this.enqueue(run, change.kind === 'published' ? 'new' : 'changed', false);
      }
    });
    link.onReconnected(() => this.enqueue(run, 'changed', true));
    link.onClosed(() => {
      void this.open(run, true);
    });

    void this.open(run, false);
  }

  /** Fecha a conexão e zera o que sabia. */
  stop(): void {
    const run = this.run;
    if (!run) {
      return;
    }
    this.run = null;
    this.count.set(0);
    void run.link.stop().catch(() => undefined);
  }

  /** O mural aberto sabe a contagem mais fresca (acabou de marcar um aviso como lido): a bolinha a segue. */
  setUnread(unread: number): void {
    this.count.set(unread);
  }

  /**
   * Abre a conexão; se não abre, espera, renova a sessão (o token pode ter vencido, motivo comum da recusa) e tenta de novo, até
   * a sessão não se sustentar. Depois de aberta, busca as contagens: no começo só as guarda, depois de uma queda as publica,
   * porque pode ter havido mudança no meio.
   */
  private async open(run: Run, afterDrop: boolean): Promise<void> {
    let mustWait = afterDrop;
    for (;;) {
      if (mustWait) {
        await new Promise<void>((resolve) => setTimeout(resolve, this.retryMs));
        if (this.run !== run || !(await this.session.renew()) || this.run !== run) {
          return;
        }
      }
      try {
        await run.link.start();
        break;
      } catch {
        mustWait = true;
      }
    }
    if (this.run !== run) {
      return;
    }
    if (afterDrop) {
      this.enqueue(run, 'changed', true);
      return;
    }
    const counters = await this.fetchCounters();
    if (this.run === run && counters) {
      this.count.set(counters.unread);
    }
  }

  private enqueue(run: Run, kind: MuralUpdate['kind'], resync: boolean): void {
    if (this.run !== run) {
      return;
    }
    run.queued = {
      kind: run.queued?.kind === 'new' || kind === 'new' ? 'new' : 'changed',
      resync: (run.queued?.resync ?? true) && resync,
    };
    if (!run.fetching) {
      void this.drain(run);
    }
  }

  /** Uma busca por vez: o que chega durante ela espera e vai numa busca só, com a mudança mais forte. */
  private async drain(run: Run): Promise<void> {
    run.fetching = true;
    while (run.queued && this.run === run) {
      const { kind, resync } = run.queued;
      run.queued = null;
      const counters = await this.fetchCounters();
      if (this.run !== run) {
        break;
      }
      if (counters) {
        this.count.set(counters.unread);
      }
      this.changes.next({ kind, resync, counters });
    }
    run.fetching = false;
  }

  /** A busca leve: a primeira página, de um aviso só, que já traz as contagens do mural inteiro. */
  private async fetchCounters(): Promise<MuralCounters | null> {
    try {
      const page = await firstValueFrom(
        this.api.listMine({ page: 1, pageSize: 1, unreadOnly: false, category: null }),
      );
      return { unread: page.unread, unreadByCategory: page.unreadByCategory };
    } catch {
      return null;
    }
  }
}
