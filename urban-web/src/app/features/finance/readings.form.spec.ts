import { FormControl } from '@angular/forms';
import { meterReading, monthReadings, unitReadings } from '../../testing/finance.fixtures';
import {
  ReadingTexts,
  changedReadings,
  consumptionText,
  hasChanges,
  fillReadingsForm,
  readingField,
  readingTexts,
  readingsForm,
} from './readings.form';

function texts(overrides: Partial<ReadingTexts> = {}): ReadingTexts {
  return {
    waterPrevious: '0',
    waterCurrent: '',
    gasPrevious: '0',
    gasCurrent: '',
    ...overrides,
  };
}

describe('readingTexts', () => {
  it('starts a unit without readings at zero, with the current empty', () => {
    expect(readingTexts(unitReadings())).toEqual(texts());
  });

  it('suggests the current reading of last month as the previous one', () => {
    const unit = unitReadings({ suggestedWaterPrevious: 98.5, suggestedGasPrevious: 12 });

    expect(readingTexts(unit)).toEqual(texts({ waterPrevious: '98,5', gasPrevious: '12' }));
  });

  it('shows the saved reading instead of the suggestion', () => {
    const unit = unitReadings({
      water: meterReading({ previous: 100, current: 112.5 }),
      gas: meterReading({ previous: 3, current: 4.25 }),
      suggestedWaterPrevious: 98,
    });

    expect(readingTexts(unit)).toEqual({
      waterPrevious: '100',
      waterCurrent: '112,5',
      gasPrevious: '3',
      gasCurrent: '4,25',
    });
  });
});

describe('readingField', () => {
  const check = (text: string, required: boolean): unknown =>
    readingField(required)(new FormControl(text, { nonNullable: true }));

  it.each(['0', '12,5', '1234,567', '9999999,999', '1.234,5'])('accepts %s', (text) => {
    expect(check(text, true)).toBeNull();
  });

  it.each(['abc', '-1', '1,2345', '10000000', '10000000,001'])('refuses %s', (text) => {
    expect(check(text, true)).toEqual({ decimal: true });
  });

  it('refuses an empty previous reading but accepts an empty current one', () => {
    expect(check('', true)).toEqual({ decimal: true });
    expect(check('  ', false)).toBeNull();
  });
});

describe('readingsForm', () => {
  it('has one row per unit, filled with the texts of the unit', () => {
    const data = monthReadings({
      units: [
        unitReadings({ number: '101', water: meterReading({ previous: 10, current: 15 }) }),
        unitReadings({ number: '102' }),
      ],
    });

    const form = readingsForm(data);

    expect(form.controls.units.length).toBe(2);
    expect(form.controls.units.at(0).getRawValue()).toEqual(
      texts({ waterPrevious: '10', waterCurrent: '15' }),
    );
    expect(form.valid).toBe(true);
    expect(form.pristine).toBe(true);
  });

  it('marks a current reading below the previous one on that row only', () => {
    const form = readingsForm(monthReadings());
    const row = form.controls.units.at(0);

    row.patchValue({ waterPrevious: '100', waterCurrent: '99,9' });

    expect(row.hasError('waterOrder')).toBe(true);
    expect(row.hasError('gasOrder')).toBe(false);
    expect(form.controls.units.at(1).valid).toBe(true);
    expect(form.invalid).toBe(true);
  });

  it('accepts a current reading equal to the previous one', () => {
    const form = readingsForm(monthReadings());

    form.controls.units.at(0).patchValue({ gasPrevious: '5', gasCurrent: '5' });

    expect(form.valid).toBe(true);
  });

  it('does not compare while a reading is empty or not a number', () => {
    const form = readingsForm(monthReadings());
    const row = form.controls.units.at(0);

    row.patchValue({ waterPrevious: '100', waterCurrent: '' });
    expect(row.hasError('waterOrder')).toBe(false);

    row.patchValue({ waterCurrent: 'abc' });
    expect(row.hasError('waterOrder')).toBe(false);
    expect(row.controls.waterCurrent.hasError('decimal')).toBe(true);
  });
});

describe('fillReadingsForm', () => {
  it('replaces the rows with the ones of the new data and starts pristine again', () => {
    const form = readingsForm(monthReadings());
    form.controls.units.at(0).patchValue({ waterCurrent: '7' });

    fillReadingsForm(
      form,
      monthReadings({ units: [unitReadings({ number: '201', suggestedWaterPrevious: 3 })] }),
    );

    expect(form.controls.units.length).toBe(1);
    expect(form.controls.units.at(0).getRawValue().waterPrevious).toBe('3');
    expect(form.pristine).toBe(true);
  });

  it('locks every field when the month is closed and unlocks them when it is open', () => {
    const form = readingsForm(monthReadings());

    fillReadingsForm(form, monthReadings({ isClosed: true }));
    expect(form.disabled).toBe(true);

    fillReadingsForm(form, monthReadings({ isClosed: false }));
    expect(form.enabled).toBe(true);
  });
});

describe('hasChanges', () => {
  const unit = unitReadings({ suggestedWaterPrevious: 100, suggestedGasPrevious: 3 });

  it('is false while the fields show what the month brings', () => {
    expect(hasChanges(monthReadings({ units: [unit] }), [readingTexts(unit)])).toBe(false);
  });

  it('is true for any edited field, even one that is not a valid number yet', () => {
    const data = monthReadings({ units: [unit] });

    expect(hasChanges(data, [{ ...readingTexts(unit), waterCurrent: 'abc' }])).toBe(true);
    expect(hasChanges(data, [{ ...readingTexts(unit), waterPrevious: '' }])).toBe(true);
  });

  it('looks at the gas fields only when the month shows gas', () => {
    const edited = [{ ...readingTexts(unit), gasCurrent: '4' }];

    expect(hasChanges(monthReadings({ hasGas: false, units: [unit] }), edited)).toBe(false);
    expect(hasChanges(monthReadings({ hasGas: true, units: [unit] }), edited)).toBe(true);
  });
});

describe('changedReadings', () => {
  const unit = unitReadings({ unitId: 'u1', suggestedWaterPrevious: 100 });
  const data = monthReadings({ units: [unit] });

  it('sends nothing when nothing changed', () => {
    expect(changedReadings(data, [readingTexts(unit)])).toEqual([]);
  });

  it('sends a new reading with its previous reading', () => {
    const rows = [{ ...readingTexts(unit), waterCurrent: '112,5' }];

    expect(changedReadings(data, rows)).toEqual([
      { unitId: 'u1', service: 'water', previous: 100, current: 112.5 },
    ]);
  });

  it('sends the previous reading changed by hand, as the first reading of a meter', () => {
    const rows = [{ ...readingTexts(unit), waterPrevious: '1.250,5', waterCurrent: '1.300' }];

    expect(changedReadings(data, rows)).toEqual([
      { unitId: 'u1', service: 'water', previous: 1250.5, current: 1300 },
    ]);
  });

  it('sends a corrected reading and leaves the unchanged ones out', () => {
    const saved = monthReadings({
      units: [
        unitReadings({
          unitId: 'u1',
          water: meterReading({ previous: 100, current: 110 }),
          gas: meterReading({ previous: 5, current: 6 }),
        }),
      ],
    });
    const rows = [{ ...readingTexts(saved.units[0]), waterCurrent: '111' }];

    expect(changedReadings(saved, rows)).toEqual([
      { unitId: 'u1', service: 'water', previous: 100, current: 111 },
    ]);
  });

  it('asks to remove a saved reading that was emptied', () => {
    const saved = monthReadings({
      units: [unitReadings({ unitId: 'u1', water: meterReading({ previous: 1, current: 2 }) })],
    });
    const rows = [{ ...readingTexts(saved.units[0]), waterCurrent: '' }];

    expect(changedReadings(saved, rows)).toEqual([
      { unitId: 'u1', service: 'water', current: null },
    ]);
  });

  it('ignores a previous reading changed while there is no current reading to save', () => {
    const rows = [{ ...readingTexts(unit), waterPrevious: '50' }];

    expect(changedReadings(data, rows)).toEqual([]);
  });

  it('does not send gas while the month does not charge gas, but keeps saved gas readings editable', () => {
    const withoutGas = [{ ...readingTexts(unit), gasPrevious: '0', gasCurrent: '4' }];
    expect(changedReadings(data, withoutGas)).toEqual([]);

    const charged = monthReadings({ hasGas: true, units: [unit] });
    expect(changedReadings(charged, withoutGas)).toEqual([
      { unitId: 'u1', service: 'gas', previous: 0, current: 4 },
    ]);
  });

  it('keeps the order of the units', () => {
    const two = monthReadings({
      units: [unitReadings({ unitId: 'a' }), unitReadings({ unitId: 'b' })],
    });
    const rows = [
      { ...readingTexts(two.units[0]) },
      { ...readingTexts(two.units[1]), waterCurrent: '3' },
    ];

    expect(changedReadings(two, rows).map((reading) => reading.unitId)).toEqual(['b']);
  });
});

describe('consumptionText', () => {
  it('shows the difference between the readings in cubic meters', () => {
    expect(consumptionText('100', '112,5')).toBe('12,5 m³');
    expect(consumptionText('0,1', '0,3')).toBe('0,2 m³');
    expect(consumptionText('1.000', '1.250,125')).toBe('250,125 m³');
    expect(consumptionText('5', '5')).toBe('0 m³');
  });

  it('shows nothing while a reading is empty, invalid or the current one is lower', () => {
    expect(consumptionText('100', '')).toBeNull();
    expect(consumptionText('', '10')).toBeNull();
    expect(consumptionText('100', 'abc')).toBeNull();
    expect(consumptionText('100', '99')).toBeNull();
  });
});
