import { FormControl } from '@angular/forms';
import { birthDate, isMinorBirth } from './birth-date.validator';

function check(value: string, today: Date = new Date(2026, 9, 29, 12)) {
  return birthDate(() => today)(new FormControl(value, { nonNullable: true }));
}

describe('birthDate', () => {
  it('deixa o campo vazio para o validador de obrigatoriedade', () => {
    expect(check('')).toBeNull();
  });

  it.each(['15/06/1980', '29/10/2008', '29/10/1906', '31/12/2000', '01/01/1950'])(
    'aceita "%s" em 29/10/2026',
    (value) => {
      expect(check(value)).toBeNull();
    },
  );

  it.each(['30/10/2008', '29/10/2009', '15/06/2020', '28/10/2026'])(
    'avisa que quem nasceu em "%s" ainda não tem 18 anos',
    (value) => {
      expect(check(value)).toEqual({ underAge: true });
    },
  );

  it.each([
    '28/10/1906',
    '01/01/1900',
    '30/10/2026',
    '01/01/2030',
    '31/02/2000',
    '29/02/2007',
    '00/10/2000',
    '10/00/2000',
    '10/13/2000',
    '15/06/200',
    '15/06',
    '15/06/20000',
    'aa/bb/cccc',
    '   ',
  ])('recusa "%s" como data inválida', (value) => {
    expect(check(value)).toEqual({ date: true });
  });

  it('conta os 18 anos de quem nasceu em 29 de fevereiro só a partir de 1º de março', () => {
    expect(check('29/02/2008', new Date(2026, 1, 28))).toEqual({ underAge: true });
    expect(check('29/02/2008', new Date(2026, 2, 1))).toBeNull();
    expect(check('29/02/2008', new Date(2026, 0, 31))).toEqual({ underAge: true });
  });

  it('aceita 29 de fevereiro nos anos bissextos', () => {
    expect(check('29/02/2000')).toBeNull();
  });

  it('usa o dia 28 de fevereiro como limite dos 120 anos quando o dia 29 não existe lá', () => {
    const leapDay = new Date(2020, 1, 29);

    expect(check('28/02/1900', leapDay)).toBeNull();
    expect(check('27/02/1900', leapDay)).toEqual({ date: true });
  });

  it('lê a data de hoje do relógio informado, por padrão o do navegador', () => {
    const validator = birthDate();
    const year = new Date().getFullYear();
    const bornIn = (born: number): FormControl<string> =>
      new FormControl(`15/06/${born}`, { nonNullable: true });

    expect(validator(bornIn(year - 30))).toBeNull();
    expect(validator(bornIn(year - 10))).toEqual({ underAge: true });
  });
});

describe('birthDate permitindo menores (dependentes)', () => {
  const today = new Date(2026, 9, 29, 12);
  const allowing = (value: string, now: Date = today) =>
    birthDate(() => now, { allowMinor: true })(new FormControl(value, { nonNullable: true }));

  it.each(['30/10/2008', '15/06/2020', '28/10/2026', '29/10/2026', '15/06/1980'])(
    'aceita "%s" em 29/10/2026',
    (value) => {
      expect(allowing(value)).toBeNull();
    },
  );

  it.each(['30/10/2026', '01/01/2030', '28/10/1906', '31/02/2000', '15/06', 'aa/bb/cccc'])(
    'continua recusando "%s" como data inválida',
    (value) => {
      expect(allowing(value)).toEqual({ date: true });
    },
  );

  it('deixa o campo vazio para o validador de obrigatoriedade', () => {
    expect(allowing('')).toBeNull();
  });
});

describe('isMinorBirth', () => {
  const today = new Date(2026, 9, 29, 12);

  it.each([
    ['30/10/2008', true],
    ['15/06/2020', true],
    ['29/10/2026', true],
    ['29/10/2008', false],
    ['15/06/1980', false],
  ])('quem nasceu em %s em 29/10/2026: menor = %s', (value, expected) => {
    expect(isMinorBirth(value, () => today)).toBe(expected);
  });

  it('conta os 18 anos de quem nasceu em 29 de fevereiro a partir de 1º de março', () => {
    expect(isMinorBirth('29/02/2008', () => new Date(2026, 1, 28))).toBe(true);
    expect(isMinorBirth('29/02/2008', () => new Date(2026, 2, 1))).toBe(false);
  });

  it.each(['', '15/06', '31/02/2000', '30/10/2026', 'aa/bb/cccc'])(
    'data incompleta ou inválida ("%s") não é menor: valem as regras do adulto até a data ficar certa',
    (value) => {
      expect(isMinorBirth(value, () => today)).toBe(false);
    },
  );
});
