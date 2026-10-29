import { BRAZILIAN_STATES } from './brazilian-states';

describe('BRAZILIAN_STATES', () => {
  it('lista as 26 UFs e o Distrito Federal em ordem alfabética', () => {
    expect(BRAZILIAN_STATES).toHaveLength(27);
    expect([...BRAZILIAN_STATES].sort()).toEqual([...BRAZILIAN_STATES]);
    expect(new Set(BRAZILIAN_STATES).size).toBe(27);
  });

  it('traz as mesmas UFs que a API aceita', () => {
    expect([...BRAZILIAN_STATES].sort()).toEqual(
      [
        'AC',
        'AL',
        'AP',
        'AM',
        'BA',
        'CE',
        'DF',
        'ES',
        'GO',
        'MA',
        'MT',
        'MS',
        'MG',
        'PA',
        'PB',
        'PR',
        'PE',
        'PI',
        'RJ',
        'RN',
        'RS',
        'RO',
        'RR',
        'SC',
        'SP',
        'SE',
        'TO',
      ].sort(),
    );
  });
});
