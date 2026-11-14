import { FormControl } from '@angular/forms';
import { entryDate } from './resident-field.validators';

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
