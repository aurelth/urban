import { FormControl } from '@angular/forms';
import { requiredText } from './required-text.validator';

function check(value: string) {
  return requiredText(new FormControl(value, { nonNullable: true }));
}

describe('requiredText', () => {
  it.each(['a', ' a ', 'Rua das Flores'])('aceita "%s"', (value) => {
    expect(check(value)).toBeNull();
  });

  it.each(['', ' ', '   ', '\t\n'])('trata "%s" como não preenchido', (value) => {
    expect(check(value)).toEqual({ required: true });
  });
});
