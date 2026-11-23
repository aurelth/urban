import { Injectable, InjectionToken, Signal, signal } from '@angular/core';
import { Observable, Subject } from 'rxjs';
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

/**
 * O mural ao vivo: mantém a conexão com o servidor enquanto a pessoa está logada e conta os avisos não lidos para o menu. A cada
 * mudança (e a cada reconexão) busca as contagens e as publica em `updates`, que o mural aberto usa para mostrar a faixa
 * "Há avisos novos" sem mexer na lista.
 */
@Injectable({ providedIn: 'root' })
export class MuralLive {
  private readonly count = signal(0);
  private readonly changes = new Subject<MuralUpdate>();

  /** Quantos avisos não lidos há, para a bolinha do menu. */
  readonly unread: Signal<number> = this.count.asReadonly();

  readonly updates: Observable<MuralUpdate> = this.changes.asObservable();

  /** Abre a conexão e busca as contagens. Chamar de novo com a conexão aberta não faz nada. */
  start(): void {
    // implementação no patch seguinte
  }

  /** Fecha a conexão e zera o que sabia. */
  stop(): void {
    // implementação no patch seguinte
  }

  /** O mural aberto sabe a contagem mais fresca (acabou de marcar um aviso como lido): a bolinha a segue. */
  setUnread(unread: number): void {
    void unread;
  }
}
