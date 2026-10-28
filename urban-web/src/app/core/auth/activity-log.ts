import { Injectable } from '@angular/core';

const STORAGE_KEY = 'urban.last-activity';

/** Gravar no armazenamento a cada movimento do mouse seria desperdício: no máximo a cada 5 s. */
const WRITE_INTERVAL_MS = 5_000;

/**
 * Registro do instante da última atividade da pessoa neste navegador.
 *
 * O instante também vai para o localStorage, que é compartilhado entre as abas: assim uma aba
 * parada não encerra a sessão de quem está usando outra aba (o cookie de renovação é um só).
 * Se o armazenamento estiver bloqueado, vale só a memória desta aba.
 */
@Injectable({ providedIn: 'root' })
export class ActivityLog {
  private last = 0;
  private lastWrite = Number.NEGATIVE_INFINITY;

  touch(now: number, force = false): void {
    this.last = now;
    if (force || now - this.lastWrite >= WRITE_INTERVAL_MS) {
      this.lastWrite = now;
      this.write(now);
    }
  }

  /** O mais recente entre esta aba e o que as outras abas gravaram. */
  lastActivity(): number {
    return Math.max(this.last, this.read());
  }

  private read(): number {
    try {
      const stored = Number(localStorage.getItem(STORAGE_KEY));
      return Number.isFinite(stored) ? stored : 0;
    } catch {
      return 0;
    }
  }

  private write(value: number): void {
    try {
      localStorage.setItem(STORAGE_KEY, String(value));
    } catch {
      // Armazenamento bloqueado (navegação privativa, política do navegador): segue só na memória.
    }
  }
}
