/** Tempo sem atividade até a sessão ser encerrada no navegador. */
export const IDLE_LIMIT_MS = 60 * 60 * 1000;

/** Quanto tempo antes do limite a pessoa é avisada. */
export const IDLE_WARNING_MS = 60 * 1000;

export type IdlePhase = 'active' | 'warning' | 'expired';

export interface IdleState {
  phase: IdlePhase;
  /** Segundos que faltam para o limite (arredondado para cima, nunca negativo). */
  secondsLeft: number;
}

/**
 * Função pura: dado o instante atual e o da última atividade, diz em que fase a sessão está.
 * Se o relógio voltar no tempo (now < lastActivity), conta como atividade recente.
 */
export function idleState(
  now: number,
  lastActivity: number,
  limit: number = IDLE_LIMIT_MS,
  warning: number = IDLE_WARNING_MS,
): IdleState {
  const remaining = limit - (now - lastActivity);
  const secondsLeft = Math.max(0, Math.ceil(remaining / 1000));
  if (remaining <= 0) {
    return { phase: 'expired', secondsLeft };
  }
  return { phase: remaining <= warning ? 'warning' : 'active', secondsLeft };
}
