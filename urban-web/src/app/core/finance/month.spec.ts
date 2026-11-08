import {
  addMonths,
  currentMonth,
  formatMonth,
  isMonth,
  monthOptions,
  monthTitle,
  canOpenMonth,
} from './month';

describe('currentMonth', () => {
  it('follows the Brazilian clock (UTC-3), not UTC', () => {
    expect(currentMonth(new Date('2026-11-01T02:59:00Z'))).toBe('2026-10');
    expect(currentMonth(new Date('2026-11-01T03:00:00Z'))).toBe('2026-11');
    expect(currentMonth(new Date('2027-01-01T01:00:00Z'))).toBe('2026-12');
  });
});

describe('addMonths', () => {
  it.each([
    ['2026-11', 1, '2026-12'],
    ['2026-12', 1, '2027-01'],
    ['2026-01', -1, '2025-12'],
    ['2026-11', 0, '2026-11'],
    ['2026-11', -14, '2025-09'],
    ['2026-11', 25, '2028-12'],
  ])('%s %i months is %s', (month, delta, expected) => {
    expect(addMonths(month, delta)).toBe(expected);
  });
});

describe('isMonth', () => {
  it.each(['2026-11', '2000-01', '2200-12'])('accepts %s', (text) => {
    expect(isMonth(text)).toBe(true);
  });

  it.each(['2026-13', '2026-00', '2026-1', '26-11', 'novembro', '', '1999-12', '2201-01'])(
    'refuses "%s"',
    (text) => {
      expect(isMonth(text)).toBe(false);
    },
  );
});

describe('formatMonth and monthTitle', () => {
  it('writes the short month for tables', () => {
    expect(formatMonth('2026-11')).toBe('11/2026');
  });

  it('writes the month in full, capitalized, for headings', () => {
    expect(monthTitle('2026-11')).toBe('Novembro de 2026');
    expect(monthTitle('2027-03')).toBe('Março de 2027');
  });
});

describe('canOpenMonth', () => {
  const now = new Date('2026-11-15T12:00:00Z');

  it('allows the current month, the past and the next month only', () => {
    expect(canOpenMonth('2026-12', now)).toBe(true);
    expect(canOpenMonth('2026-11', now)).toBe(true);
    expect(canOpenMonth('2020-01', now)).toBe(true);
    expect(canOpenMonth('2027-01', now)).toBe(false);
  });
});

describe('monthOptions', () => {
  it('lists a range of months, oldest first, with the title as label', () => {
    expect(monthOptions('2026-11', '2027-01')).toEqual([
      { value: '2026-11', label: 'Novembro de 2026' },
      { value: '2026-12', label: 'Dezembro de 2026' },
      { value: '2027-01', label: 'Janeiro de 2027' },
    ]);
  });

  it('is empty when the range is backwards', () => {
    expect(monthOptions('2027-01', '2026-11')).toEqual([]);
  });
});
