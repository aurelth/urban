import { formatRequestedAt } from './requested-at';

describe('formatRequestedAt', () => {
  it('mostra a data e a hora de Brasília, com dois dígitos', () => {
    expect(formatRequestedAt('2026-11-13T04:23:00+00:00')).toBe('13/11/2026 às 01:23');
  });

  it('muda o dia quando a hora de Brasília cai no dia anterior', () => {
    expect(formatRequestedAt('2026-11-14T01:05:00+00:00')).toBe('13/11/2026 às 22:05');
  });

  it('mostra meia-noite como 00:00', () => {
    expect(formatRequestedAt('2026-11-13T03:00:00+00:00')).toBe('13/11/2026 às 00:00');
  });

  it('devolve o texto como veio quando a data é ilegível', () => {
    expect(formatRequestedAt('ontem')).toBe('ontem');
  });
});
