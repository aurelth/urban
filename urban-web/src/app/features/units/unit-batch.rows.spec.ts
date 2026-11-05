import { BatchRow } from './unit-batch.generator';
import {
  findDuplicateRows,
  splitRemainingFraction,
  toBatchPayload,
  validateRow,
} from './unit-batch.rows';

function row(overrides: Partial<BatchRow> = {}): BatchRow {
  return { block: 'A', number: '101', type: 'apartment', fraction: '', area: '', ...overrides };
}

describe('validateRow', () => {
  it('linha completa ou so com o numero nao tem problema', () => {
    expect(validateRow(row())).toEqual({});
    expect(validateRow(row({ fraction: '1,25', area: '52,5' }))).toEqual({});
    expect(validateRow(row({ block: '' }))).toEqual({});
  });

  it('exige o numero, ignorando espacos', () => {
    expect(validateRow(row({ number: '   ' }))).toEqual({ number: 'Informe o número da unidade' });
  });

  it('limita o numero a 20 e o bloco a 30 caracteres', () => {
    expect(validateRow(row({ number: '1'.repeat(21) })).number).toBe(
      'O número pode ter até 20 caracteres',
    );
    expect(validateRow(row({ number: '1'.repeat(20) }))).toEqual({});
    expect(validateRow(row({ block: 'B'.repeat(31) })).block).toBe(
      'O bloco pode ter até 30 caracteres',
    );
    expect(validateRow(row({ block: 'B'.repeat(30) }))).toEqual({});
  });

  it('explica a fracao e a area invalidas', () => {
    expect(validateRow(row({ fraction: 'abc', area: 'x' }))).toEqual({
      fraction: 'Informe uma fração maior que 0% e até 100%, com até 6 casas decimais',
      area: 'Informe uma área maior que 0 e até 99.999,99 m², com até 2 casas decimais',
    });
  });
});

describe('findDuplicateRows', () => {
  it('marca todas as linhas que repetem bloco e numero, sem diferenciar maiusculas', () => {
    const rows = [
      row({ block: 'A', number: '101' }),
      row({ block: 'A', number: '102' }),
      row({ block: 'a', number: '101' }),
      row({ block: 'B', number: '101' }),
    ];

    expect([...findDuplicateRows(rows)]).toEqual([0, 2]);
  });

  it('ignora espacos nas pontas e trata bloco vazio como bloco vazio', () => {
    const rows = [
      row({ block: '', number: '5' }),
      row({ block: '  ', number: ' 5 ' }),
      row({ block: 'A', number: '5' }),
    ];

    expect([...findDuplicateRows(rows)]).toEqual([0, 1]);
  });

  it('linhas sem numero nao contam como repetidas', () => {
    expect(findDuplicateRows([row({ number: '' }), row({ number: ' ' })]).size).toBe(0);
  });

  it('sem repeticao, o conjunto e vazio', () => {
    expect(findDuplicateRows([row({ number: '1' }), row({ number: '2' })]).size).toBe(0);
  });
});

describe('toBatchPayload', () => {
  it('converte porcentagem em fracao, texto em numero e bloco vazio em nulo', () => {
    const payload = toBatchPayload([
      row({ block: ' A ', number: ' 101 ', fraction: '1,25', area: '52,5' }),
      row({ block: '', number: '102', type: 'house' }),
    ]);

    expect(payload).toEqual([
      { block: 'A', number: '101', type: 'apartment', idealFraction: 0.0125, area: 52.5 },
      { block: null, number: '102', type: 'house', idealFraction: null, area: null },
    ]);
  });

  it('mantem a ordem da pre-visualizacao, que e a posicao devolvida nos erros da API', () => {
    const payload = toBatchPayload([
      row({ number: '3' }),
      row({ number: '1' }),
      row({ number: '2' }),
    ]);

    expect(payload.map((unit) => unit.number)).toEqual(['3', '1', '2']);
  });
});

describe('splitRemainingFraction', () => {
  it('divide o que falta para 100% igualmente entre as linhas sem fracao', () => {
    const rows = [
      row({ number: '1' }),
      row({ number: '2' }),
      row({ number: '3' }),
      row({ number: '4' }),
    ];

    expect(splitRemainingFraction(rows, 0).map((item) => item.fraction)).toEqual([
      '25',
      '25',
      '25',
      '25',
    ]);
  });

  it('desconta o que as unidades ja cadastradas ocupam', () => {
    const rows = [row({ number: '1' }), row({ number: '2' })];

    expect(splitRemainingFraction(rows, 0.5).map((item) => item.fraction)).toEqual(['25', '25']);
  });

  it('desconta tambem as linhas que ja tem fracao e nao mexe nelas', () => {
    const rows = [row({ number: '1', fraction: '40' }), row({ number: '2' }), row({ number: '3' })];

    expect(splitRemainingFraction(rows, 0.2).map((item) => item.fraction)).toEqual([
      '40',
      '20',
      '20',
    ]);
  });

  it('arredonda para baixo, para a soma nunca passar de 100%', () => {
    const rows = [row({ number: '1' }), row({ number: '2' }), row({ number: '3' })];

    const result = splitRemainingFraction(rows, 0);

    expect(result.map((item) => item.fraction)).toEqual(['33,333333', '33,333333', '33,333333']);
  });

  it('nao muda nada quando nao ha linha sem fracao ou nao sobra espaco', () => {
    const full = [row({ number: '1', fraction: '10' })];
    const rows = [row({ number: '1' })];

    expect(splitRemainingFraction(full, 0)).toEqual(full);
    expect(splitRemainingFraction(rows, 1)).toEqual(rows);
    expect(splitRemainingFraction(rows, 1.5)).toEqual(rows);
  });

  it('trata fracao digitada invalida como zero e nao altera a linha', () => {
    const rows = [row({ number: '1', fraction: 'abc' }), row({ number: '2' })];

    expect(splitRemainingFraction(rows, 0).map((item) => item.fraction)).toEqual(['abc', '100']);
  });

  it('nao altera a lista original', () => {
    const rows = [row({ number: '1' })];

    splitRemainingFraction(rows, 0);

    expect(rows[0].fraction).toBe('');
  });
});
