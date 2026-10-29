import { FormControl } from '@angular/forms';
import { fullName } from './full-name.validator';

function check(value: string) {
  return fullName(new FormControl(value, { nonNullable: true }));
}

describe('fullName', () => {
  it.each([
    'Maria Silva',
    'Maria  da   Silva',
    "Ana D'Ávila-Souza Jr.",
    'João Pedro Álvares',
    '  Maria Silva  ',
    'Al Li',
    'Maria de S. Oliveira',
    `Ab ${'c'.repeat(117)}`,
  ])('aceita "%s"', (value) => {
    expect(check(value)).toBeNull();
  });

  it('deixa o campo vazio para o validador de obrigatoriedade', () => {
    expect(check('')).toBeNull();
  });

  it.each([
    '   ',
    'Maria',
    'M Silva',
    'Maria S',
    'Maria Silva1',
    'Maria_Silva Souza',
    'Maria <b>Silva',
    `Ab ${'c'.repeat(118)}`,
    'Maria\tSilva\u0007',
  ])('recusa "%s"', (value) => {
    expect(check(value)).toEqual({ fullName: true });
  });
});
