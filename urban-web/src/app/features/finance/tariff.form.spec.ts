import { FormControl } from '@angular/forms';
import { consumptionTariff, tariffBand } from '../../testing/finance.fixtures';
import {
  MAX_BANDS,
  NO_LIMIT_TEXT,
  addBand,
  ascendingLimits,
  bandArray,
  copyBands,
  decimalField,
  gasPricePerCubicMeter,
  removeBand,
  toBands,
} from './tariff.form';

function rowValues(array: ReturnType<typeof bandArray>): string[][] {
  return array.controls.map((row) => {
    const { upTo, fixed, price } = row.getRawValue();
    return [upTo, fixed, price];
  });
}

describe('bandArray', () => {
  it('starts with a single open band when there is no tariff', () => {
    const array = bandArray(null);

    expect(rowValues(array)).toEqual([[NO_LIMIT_TEXT, '0,00', '']]);
    expect(array.at(0).controls.upTo.disabled).toBe(true);
  });

  it('shows the bands of a tariff as the person types them, the last one without limit', () => {
    const array = bandArray(consumptionTariff().water);

    expect(rowValues(array)).toEqual([
      ['10', '28,41', '0,00'],
      ['20', '0,00', '3,50'],
      [NO_LIMIT_TEXT, '0,00', '5,10'],
    ]);
    expect(array.at(0).controls.upTo.enabled).toBe(true);
    expect(array.at(2).controls.upTo.disabled).toBe(true);
  });
});

describe('addBand and removeBand', () => {
  it('adds a band at the end and gives the old last band a limit to fill in', () => {
    const array = bandArray(null);

    addBand(array);

    expect(rowValues(array)).toEqual([
      ['', '0,00', ''],
      [NO_LIMIT_TEXT, '0,00', ''],
    ]);
    expect(array.at(0).controls.upTo.enabled).toBe(true);
    expect(array.at(1).controls.upTo.disabled).toBe(true);
  });

  it(`stops at ${String(MAX_BANDS)} bands`, () => {
    const array = bandArray(null);

    for (let index = 0; index < 10; index++) {
      addBand(array);
    }

    expect(array.length).toBe(MAX_BANDS);
  });

  it('removes a band from the middle and keeps the others', () => {
    const array = bandArray(consumptionTariff().water);

    removeBand(array, 1);

    expect(rowValues(array)).toEqual([
      ['10', '28,41', '0,00'],
      [NO_LIMIT_TEXT, '0,00', '5,10'],
    ]);
  });

  it('turns the band before into the open one when the last is removed', () => {
    const array = bandArray(consumptionTariff().water);

    removeBand(array, 2);

    expect(array.length).toBe(2);
    expect(array.at(1).controls.upTo.disabled).toBe(true);
    expect(array.at(1).controls.upTo.value).toBe(NO_LIMIT_TEXT);
    expect(array.at(1).controls.price.value).toBe('3,50');
  });

  it('never removes the only band', () => {
    const array = bandArray(null);

    removeBand(array, 0);

    expect(array.length).toBe(1);
  });
});

describe('copyBands', () => {
  it('makes the target an exact copy of the source', () => {
    const source = bandArray(consumptionTariff().water);
    const target = bandArray(null);

    copyBands(target, source);

    expect(rowValues(target)).toEqual(rowValues(source));
    expect(target.at(2).controls.upTo.disabled).toBe(true);
    expect(target.at(0).controls.upTo.enabled).toBe(true);
  });

  it('does not share the controls with the source', () => {
    const source = bandArray(consumptionTariff().water);
    const target = bandArray(null);

    copyBands(target, source);
    target.at(0).controls.price.setValue('9,99');

    expect(source.at(0).controls.price.value).toBe('0,00');
  });
});

describe('toBands', () => {
  it('reads the typed values as numbers, the last band without limit', () => {
    const array = bandArray(consumptionTariff().water);

    expect(toBands(array)).toEqual(consumptionTariff().water);
  });

  it('reads Brazilian numbers', () => {
    const array = bandArray(null);
    array.at(0).setValue({ upTo: NO_LIMIT_TEXT, fixed: '1.234,56', price: '8,5' });

    expect(toBands(array)).toEqual([tariffBand({ fixedAmount: 1234.56, pricePerCubicMeter: 8.5 })]);
  });

  it('refuses when some field is not a number', () => {
    const array = bandArray(consumptionTariff().water);
    array.at(1).controls.price.setValue('abc');

    expect(toBands(array)).toBeNull();
  });
});

describe('decimalField', () => {
  const rule = decimalField({ decimals: 2, max: 100 });

  function check(value: string, validator = rule): unknown {
    return validator(new FormControl(value, { nonNullable: true }));
  }

  it.each(['0', '0,00', '10', '99,99', '100', '1,5'])('accepts "%s"', (value) => {
    expect(check(value)).toBeNull();
  });

  it.each(['', 'abc', '-1', '100,01', '1,234'])('refuses "%s"', (value) => {
    expect(check(value)).toEqual({ decimal: true });
  });

  it('can require a value above zero', () => {
    const positive = decimalField({ decimals: 3, max: 100000, positive: true });

    expect(check('0', positive)).toEqual({ decimal: true });
    expect(check('0,001', positive)).toBeNull();
    expect(check('100000', positive)).toBeNull();
    expect(check('100001', positive)).toEqual({ decimal: true });
  });
});

describe('ascendingLimits', () => {
  function limits(...values: string[]): ReturnType<typeof bandArray> {
    const array = bandArray(null);
    for (let index = 1; index < values.length; index++) {
      addBand(array);
    }
    values.forEach((value, index) => {
      if (index < values.length - 1) {
        array.at(index).controls.upTo.setValue(value);
      }
    });
    array.setValidators(ascendingLimits);
    array.updateValueAndValidity();
    return array;
  }

  it('accepts limits that grow', () => {
    expect(limits('10', '20', '').errors).toBeNull();
  });

  it('refuses a limit equal to or below the one before', () => {
    expect(limits('10', '10', '').errors).toEqual({ ascending: true });
    expect(limits('20', '10', '').errors).toEqual({ ascending: true });
  });

  it('leaves unfilled limits to the field errors', () => {
    expect(limits('', '10', '').errors).toBeNull();
  });

  it('leaves a limit of zero to the field errors too', () => {
    expect(limits('0', '10', '').errors).toBeNull();
  });
});

describe('gasPricePerCubicMeter', () => {
  it.each([
    [2.4, 15.71, 37.7],
    [1, 10, 10],
    [2.5, 1.01, 2.53],
    [0.333, 10.01, 3.33],
    [100, 999.99, 99999],
  ])('%f kg/m³ at R$ %f per kg is R$ %f per m³', (kilos, price, expected) => {
    expect(gasPricePerCubicMeter(kilos, price)).toBe(expected);
  });
});
