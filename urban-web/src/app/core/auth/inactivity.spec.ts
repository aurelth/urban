import { IDLE_LIMIT_MS, IDLE_WARNING_MS, idleState } from './inactivity';

const MINUTE = 60_000;

describe('idleState', () => {
  it('o limite de inatividade e de 60 minutos, com aviso 1 minuto antes', () => {
    expect(IDLE_LIMIT_MS).toBe(60 * MINUTE);
    expect(IDLE_WARNING_MS).toBe(MINUTE);
  });

  it.each([
    ['logo depois da ultima atividade', 0, 'active'],
    ['quase no aviso', 59 * MINUTE - 1, 'active'],
    ['no instante do aviso', 59 * MINUTE, 'warning'],
    ['no meio do aviso', 59 * MINUTE + 30_000, 'warning'],
    ['um instante antes do limite', 60 * MINUTE - 1, 'warning'],
    ['no limite', 60 * MINUTE, 'expired'],
    ['depois do limite', 3 * 60 * MINUTE, 'expired'],
  ])('%s: fica "%s"', (_caso, idle, phase) => {
    expect(idleState(1_000_000 + idle, 1_000_000).phase).toBe(phase);
  });

  it('conta os segundos que faltam, arredondando para cima', () => {
    expect(idleState(59 * MINUTE, 0).secondsLeft).toBe(60);
    expect(idleState(59 * MINUTE + 1, 0).secondsLeft).toBe(60);
    expect(idleState(59 * MINUTE + 1_000, 0).secondsLeft).toBe(59);
    expect(idleState(60 * MINUTE - 1, 0).secondsLeft).toBe(1);
    expect(idleState(60 * MINUTE, 0).secondsLeft).toBe(0);
  });

  it('aceita outro limite e outro aviso', () => {
    expect(idleState(5_000, 0, 10_000, 3_000).phase).toBe('active');
    expect(idleState(7_000, 0, 10_000, 3_000).phase).toBe('warning');
    expect(idleState(10_000, 0, 10_000, 3_000).phase).toBe('expired');
  });

  it('trata o relogio que voltou no tempo como atividade recente', () => {
    expect(idleState(500, 1_000).phase).toBe('active');
  });
});
