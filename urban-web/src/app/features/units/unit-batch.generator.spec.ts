import {
  MAX_BATCH_UNITS,
  buildFloors,
  floorUnitNumber,
  generateFloorRows,
  generateHouseRows,
  totalUnits,
} from './unit-batch.generator';

describe('buildFloors', () => {
  it('cria o mesmo numero de unidades em cada andar do intervalo', () => {
    expect(buildFloors(1, 3, 4)).toEqual([
      { floor: 1, count: 4 },
      { floor: 2, count: 4 },
      { floor: 3, count: 4 },
    ]);
  });

  it('aceita o terreo (andar 0) e um andar so', () => {
    expect(buildFloors(0, 1, 2)).toEqual([
      { floor: 0, count: 2 },
      { floor: 1, count: 2 },
    ]);
    expect(buildFloors(5, 5, 3)).toEqual([{ floor: 5, count: 3 }]);
  });

  it.each([
    [3, 1, 2],
    [-1, 2, 2],
    [1, 100, 2],
    [1, 3, 0],
    [1, 3, 100],
    [1.5, 3, 2],
    [1, 3, 2.5],
    [Number.NaN, 3, 2],
  ])('devolve vazio quando o intervalo ou a quantidade e invalido (%s, %s, %s)', (a, b, c) => {
    expect(buildFloors(a, b, c)).toEqual([]);
  });
});

describe('floorUnitNumber', () => {
  it.each([
    [1, 1, '101'],
    [1, 10, '110'],
    [12, 3, '1203'],
    [10, 1, '1001'],
    [0, 1, '001'],
    [0, 12, '012'],
    [99, 99, '9999'],
  ])('andar %s, posicao %s vira "%s"', (floor, position, number) => {
    expect(floorUnitNumber(floor, position)).toBe(number);
  });
});

describe('generateFloorRows', () => {
  it('gera os apartamentos andar por andar, com fracao e area em branco', () => {
    const rows = generateFloorRows('A', [
      { floor: 1, count: 2 },
      { floor: 2, count: 3 },
    ]);

    expect(rows.map((row) => row.number)).toEqual(['101', '102', '201', '202', '203']);
    for (const row of rows) {
      expect(row).toMatchObject({ block: 'A', type: 'apartment', fraction: '', area: '' });
    }
  });

  it('respeita andares com quantidades diferentes e pula os que tem zero', () => {
    const rows = generateFloorRows('', [
      { floor: 0, count: 1 },
      { floor: 1, count: 0 },
      { floor: 3, count: 2 },
    ]);

    expect(rows.map((row) => row.number)).toEqual(['001', '301', '302']);
    expect(rows[0].block).toBe('');
  });

  it('tira os espacos das pontas do bloco', () => {
    expect(generateFloorRows('  B ', [{ floor: 1, count: 1 }])[0].block).toBe('B');
  });

  it('sem andares, nao gera nada', () => {
    expect(generateFloorRows('A', [])).toEqual([]);
  });
});

describe('generateHouseRows', () => {
  it('numera as casas em sequencia a partir de 1', () => {
    const rows = generateHouseRows('', 3);

    expect(rows.map((row) => row.number)).toEqual(['1', '2', '3']);
    for (const row of rows) {
      expect(row).toMatchObject({ block: '', type: 'house', fraction: '', area: '' });
    }
  });

  it('usa o bloco (rua ou quadra) informado', () => {
    expect(generateHouseRows(' Rua 2 ', 1)[0].block).toBe('Rua 2');
  });

  it.each([0, -1, 1.5, Number.NaN])('com a quantidade %s, nao gera nada', (count) => {
    expect(generateHouseRows('', count)).toEqual([]);
  });
});

describe('totalUnits', () => {
  it('soma as unidades de todos os andares', () => {
    expect(
      totalUnits([
        { floor: 1, count: 4 },
        { floor: 2, count: 0 },
        { floor: 3, count: 5 },
      ]),
    ).toBe(9);
  });

  it('sem andares, e zero', () => {
    expect(totalUnits([])).toBe(0);
  });
});

it('o limite do lote e o mesmo da API', () => {
  expect(MAX_BATCH_UNITS).toBe(500);
});
