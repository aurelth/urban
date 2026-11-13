import { formatDay } from './admin-dates';

describe('formatDay', () => {
  it('mostra o dia de Brasília, com dois dígitos', () => {
    expect(formatDay('2026-11-01T15:30:00+00:00')).toBe('01/11/2026');
  });

  it('muda o dia quando a hora de Brasília cai no dia anterior', () => {
    expect(formatDay('2026-11-14T01:05:00+00:00')).toBe('13/11/2026');
  });

  it('devolve o texto como veio quando a data é ilegível', () => {
    expect(formatDay('ontem')).toBe('ontem');
  });
});
