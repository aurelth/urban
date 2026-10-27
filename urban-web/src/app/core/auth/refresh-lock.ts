export const REFRESH_LOCK_NAME = 'urban.refresh';

/**
 * Renovar a sessão troca o cookie de renovação (rotação). Com duas abas abertas, duas renovações
 * simultâneas mandariam o mesmo cookie e a API trataria a segunda como reuso, derrubando todas as
 * sessões. O Web Locks faz uma aba esperar a outra; a que espera já manda o cookie novo.
 */
export function withRefreshLock<T>(work: () => Promise<T>): Promise<T> {
  const locks = globalThis.navigator?.locks;
  return locks ? locks.request(REFRESH_LOCK_NAME, work) : work();
}
