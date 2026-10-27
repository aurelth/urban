import { FormControl } from '@angular/forms';
import { emailFormat } from './email.validator';

describe('emailFormat', () => {
  it.each(['sindica@example.com', 'a.b+c@sub.example.com.br'])('aceita "%s"', (value) => {
    expect(emailFormat(new FormControl(value, { nonNullable: true }))).toBeNull();
  });

  it('deixa o campo vazio para o validador de obrigatoriedade', () => {
    expect(emailFormat(new FormControl('', { nonNullable: true }))).toBeNull();
  });

  it.each([
    '   ',
    '  sindica@example.com  ',
    'sem-arroba',
    'a@b',
    '@example.com',
    'a@@example.com',
    'a b@example.com',
  ])('recusa "%s"', (value) => {
    expect(emailFormat(new FormControl(value, { nonNullable: true }))).toEqual({ email: true });
  });
});
