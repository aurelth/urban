import { FormControl } from '@angular/forms';
import { displayName } from './display-name.validator';

function check(value: string) {
  return displayName(new FormControl(value, { nonNullable: true }));
}

describe('displayName', () => {
  it.each(['Ana', 'Aurel', "D'Ávila", 'Ana-Maria', 'Dr. Silva', 'Jo', '  Ana  ', 'a'.repeat(40)])(
    'aceita "%s"',
    (value) => {
      expect(check(value)).toBeNull();
    },
  );

  it('deixa o campo vazio para o validador de obrigatoriedade', () => {
    expect(check('')).toBeNull();
  });

  it.each(['   ', 'A', '. -', 'Ana1', 'Ana_', 'Ana <b>', 'a'.repeat(41), 'Ana\u0007'])(
    'recusa "%s"',
    (value) => {
      expect(check(value)).toEqual({ displayName: true });
    },
  );

  it('conta o tamanho depois de juntar os espaços repetidos', () => {
    expect(check(`${'a'.repeat(20)}      ${'b'.repeat(19)}`)).toBeNull();
    expect(check(`${'a'.repeat(20)}      ${'b'.repeat(20)}`)).toEqual({ displayName: true });
  });
});
