import { meterReading, monthReadings, unitReadings } from '../../testing/finance.fixtures';
import { coverageLines, initialMonth, showsGas } from './readings-values';

describe('showsGas', () => {
  it('shows gas when the tariff charges it', () => {
    expect(showsGas(monthReadings({ hasGas: true }))).toBe(true);
  });

  it('hides gas when the tariff does not charge it and no unit has a gas reading', () => {
    expect(showsGas(monthReadings({ hasGas: false }))).toBe(false);
  });

  it('shows gas when a saved gas reading would otherwise be hidden', () => {
    const data = monthReadings({
      hasGas: false,
      units: [unitReadings({ gas: meterReading() })],
    });

    expect(showsGas(data)).toBe(true);
  });
});

describe('coverageLines', () => {
  const units = [
    unitReadings({ water: meterReading(), gas: meterReading() }),
    unitReadings({ water: meterReading() }),
    unitReadings(),
  ];

  it('counts the units with a water reading', () => {
    const waterOnly = [units[1], units[1], units[2]];

    expect(coverageLines(monthReadings({ units: waterOnly }))).toEqual([
      'Água: 2 de 3 unidades com leitura',
    ]);
  });

  it('counts the gas too when the month shows it', () => {
    expect(coverageLines(monthReadings({ hasGas: true, units }))).toEqual([
      'Água: 2 de 3 unidades com leitura',
      'Gás: 1 de 3 unidades com leitura',
    ]);
  });

  it('writes the singular for one unit', () => {
    const one = monthReadings({ units: [unitReadings({ water: meterReading() })] });

    expect(coverageLines(one)).toEqual(['Água: 1 de 1 unidade com leitura']);
  });
});

describe('initialMonth', () => {
  const now = new Date('2026-11-15T15:00:00Z');

  it('opens at the requested month when it is valid and inside the 5-year window', () => {
    expect(initialMonth('2026-09', now)).toBe('2026-09');
    expect(initialMonth('2031-11', now)).toBe('2031-11');
  });

  it('opens at the current month when nothing valid is requested', () => {
    expect(initialMonth(null, now)).toBe('2026-11');
    expect(initialMonth('2026-13', now)).toBe('2026-11');
    expect(initialMonth('novembro', now)).toBe('2026-11');
    expect(initialMonth('2031-12', now)).toBe('2026-11');
    expect(initialMonth('2021-10', now)).toBe('2026-11');
  });
});
