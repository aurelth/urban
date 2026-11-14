import { FormControl } from '@angular/forms';
import { entryDate, exitDate } from './resident-field.validators';

const TODAY = new Date(2026, 10, 13);

function check(value: string): Record<string, unknown> | null {
  return entryDate(() => TODAY)(new FormControl(value, { nonNullable: true }));
}

describe('entryDate', () => {
  it('deixa o vazio para o Validators.required', () => {
    expect(check('')).toBeNull();
  });

  it.each(['01/03/2024', '13/11/2026', '01/01/1900', '29/02/2024'])('aceita %s', (value) => {
    expect(check(value)).toBeNull();
  });

  it('aceita até 1 ano à frente, como a API', () => {
    expect(check('13/11/2027')).toBeNull();
  });

  it('recusa um dia depois de 1 ano à frente', () => {
    expect(check('14/11/2027')).toEqual({ date: true });
  });

  it('recusa antes de 1900', () => {
    expect(check('31/12/1899')).toEqual({ date: true });
  });

  it.each(['31/02/2024', '29/02/2025', '00/01/2024', '1/1/2024', '2024-03-01', '01/03/24'])(
    'recusa a data impossível ou fora do formato %s',
    (value) => {
      expect(check(value)).toEqual({ date: true });
    },
  );
});

describe('exitDate', () => {
  /** 14/11/2026, meio-dia em Brasília (15h UTC). */
  const NOON = () => new Date('2026-11-14T15:00:00Z');
  /** 13/11/2026, 23h30 em Brasília, mas já 14/11 no UTC: a API conta o dia de Brasília. */
  const LATE_NIGHT = () => new Date('2026-11-14T02:30:00Z');

  function checkExit(
    value: string,
    start = '2024-03-01',
    clock: () => Date = NOON,
  ): Record<string, unknown> | null {
    return exitDate(start, clock)(new FormControl(value, { nonNullable: true }));
  }

  it('deixa o vazio para o Validators.required', () => {
    expect(checkExit('')).toBeNull();
  });

  it.each(['01/03/2024', '10/09/2025', '14/11/2026'])('aceita %s: da entrada até hoje', (value) => {
    expect(checkExit(value)).toBeNull();
  });

  it('recusa um dia antes da entrada', () => {
    expect(checkExit('29/02/2024')).toEqual({ beforeStart: true });
  });

  it('recusa amanhã', () => {
    expect(checkExit('15/11/2026')).toEqual({ future: true });
  });

  it('conta o dia de Brasília, não o do UTC', () => {
    expect(checkExit('13/11/2026', '2024-03-01', LATE_NIGHT)).toBeNull();
    expect(checkExit('14/11/2026', '2024-03-01', LATE_NIGHT)).toEqual({ future: true });
  });

  it.each(['31/02/2026', '1/1/2026', '2026-03-01', '01/03/26', '00/01/2026'])(
    'recusa a data impossível ou fora do formato %s',
    (value) => {
      expect(checkExit(value)).toEqual({ date: true });
    },
  );
});
