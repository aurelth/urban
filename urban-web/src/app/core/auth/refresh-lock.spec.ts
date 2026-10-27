import { jest } from '@jest/globals';
import { REFRESH_LOCK_NAME, withRefreshLock } from './refresh-lock';

function setLocks(value: unknown): void {
  Object.defineProperty(navigator, 'locks', { configurable: true, writable: true, value });
}

describe('withRefreshLock', () => {
  afterEach(() => setLocks(undefined));

  it('executa direto quando o navegador nao tem Web Locks', async () => {
    setLocks(undefined);

    await expect(withRefreshLock(() => Promise.resolve('ok'))).resolves.toBe('ok');
  });

  it('serializa pelo lock quando o navegador tem Web Locks', async () => {
    const request = jest.fn((_name: string, callback: () => Promise<string>) => callback());
    setLocks({ request });
    const work = jest.fn(() => Promise.resolve('renovado'));

    await expect(withRefreshLock(work)).resolves.toBe('renovado');

    expect(request).toHaveBeenCalledTimes(1);
    expect(request.mock.calls[0][0]).toBe(REFRESH_LOCK_NAME);
    expect(work).toHaveBeenCalledTimes(1);
  });

  it('propaga o erro do trabalho', async () => {
    setLocks(undefined);

    await expect(withRefreshLock(() => Promise.reject(new Error('falhou')))).rejects.toThrow(
      'falhou',
    );
  });
});
