import { FormControl, Validators } from '@angular/forms';
import { minorKind, unlessMinor } from './resident-minor';

function control(value: string): FormControl<string> {
  return new FormControl(value, { nonNullable: true });
}

describe('unlessMinor', () => {
  it('para o adulto vale o validador de sempre, inclusive o campo em branco', () => {
    const validator = unlessMinor(() => false, Validators.required);

    expect(validator(control(''))).toEqual({ required: true });
    expect(validator(control('Engenheira'))).toBeNull();
  });

  it('para o menor o campo em branco passa', () => {
    expect(unlessMinor(() => true, Validators.required)(control(''))).toBeNull();
    expect(unlessMinor(() => true, Validators.required)(control('   '))).toBeNull();
  });

  it('para o menor o campo preenchido continua sendo conferido', () => {
    const validator = unlessMinor(() => true, Validators.minLength(5));

    expect(validator(control('abc'))).toEqual({
      minlength: { requiredLength: 5, actualLength: 3 },
    });
    expect(validator(control('abcdef'))).toBeNull();
  });

  it('lê o "é menor" na hora de validar, não na hora de montar', () => {
    let minor = false;
    const validator = unlessMinor(() => minor, Validators.required);

    expect(validator(control(''))).toEqual({ required: true });
    minor = true;
    expect(validator(control(''))).toBeNull();
  });
});

describe('minorKind', () => {
  it('o menor só pode ser dependente', () => {
    const validator = minorKind(() => true);

    expect(validator(control('owner'))).toEqual({ minorKind: true });
    expect(validator(control('tenant'))).toEqual({ minorKind: true });
    expect(validator(control('dependent'))).toBeNull();
  });

  it('deixa o vínculo ainda não escolhido para o validador de obrigatoriedade', () => {
    expect(minorKind(() => true)(control(''))).toBeNull();
  });

  it('o adulto pode ter qualquer vínculo', () => {
    const validator = minorKind(() => false);

    expect(validator(control('owner'))).toBeNull();
    expect(validator(control('dependent'))).toBeNull();
  });
});
