import { FormControl } from '@angular/forms';
import { trimmedLength } from './text-length.validator';

function check(value: string, min = 3, max = 10) {
  return trimmedLength(min, max)(new FormControl(value, { nonNullable: true }));
}

describe('trimmedLength', () => {
  it.each(['abc', 'abcdefghij', '  abc  ', ' abcdefghij '])('aceita "%s"', (value) => {
    expect(check(value)).toBeNull();
  });

  it.each(['ab', '  ab  ', 'a'])('recusa "%s" por ser curto', (value) => {
    expect(check(value)).toEqual({ tooShort: { min: 3 } });
  });

  it.each(['abcdefghijk', '  abcdefghijk  '])('recusa "%s" por ser longo', (value) => {
    expect(check(value)).toEqual({ tooLong: { max: 10 } });
  });

  it.each(['', '   ', '\t\n'])('deixa "%s" vazio para o requiredText', (value) => {
    expect(check(value)).toBeNull();
  });

  it('conta as letras sem os espaços das pontas, mas com os do meio', () => {
    expect(check('a b', 3, 3)).toBeNull();
    expect(check(' a b ', 4, 4)).toEqual({ tooShort: { min: 4 } });
  });
});
