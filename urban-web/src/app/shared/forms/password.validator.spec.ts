import { FormControl } from '@angular/forms';
import { passwordPolicy } from './password.validator';

function check(value: string) {
  return passwordPolicy(new FormControl(value, { nonNullable: true }));
}

describe('passwordPolicy', () => {
  it.each(['Senha-segura-123', 'abcdefghi1', '1234567a89', 'ãçãçãçãçã1'])(
    'aceita "%s"',
    (value) => {
      expect(check(value)).toBeNull();
    },
  );

  it('deixa o campo vazio para o validador de obrigatoriedade', () => {
    expect(check('')).toBeNull();
  });

  it('recusa senha com menos de 10 caracteres', () => {
    expect(check('Abc123456')).toEqual({ tooShort: true });
  });

  it('aceita 128 caracteres e recusa 129', () => {
    expect(check(`${'a'.repeat(127)}1`)).toBeNull();
    expect(check(`${'a'.repeat(128)}1`)).toEqual({ tooLong: true });
  });

  it.each(['abcdefghijkl', '1234567890123', '!!!!!!!!!!!!'])(
    'exige letra e número em "%s"',
    (value) => {
      expect(check(value)).toEqual({ needsLetterAndDigit: true });
    },
  );
});
