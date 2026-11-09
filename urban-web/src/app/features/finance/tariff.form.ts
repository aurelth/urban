import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  ValidationErrors,
  ValidatorFn,
} from '@angular/forms';
import { TariffBand } from '../../core/finance/finance.models';
import { formatDecimalInput, parseDecimal } from '../../core/finance/decimal';
import { formatMoneyInput } from '../../core/finance/money';

export const MAX_BANDS = 6;
/** O que o campo "Até" mostra na última faixa, que nunca tem limite. */
export const NO_LIMIT_TEXT = 'Sem limite';

const LIMIT_DECIMALS = 3;
const LIMIT_MAX = 100_000;
const FIXED_DECIMALS = 2;
const FIXED_MAX = 9_999_999.99;
const PRICE_DECIMALS = 4;
const PRICE_MAX = 9_999.9999;
const THOUSANDTHS = 1000;
const CENTS = 100;
const HALF_THOUSANDTH = 500;

export interface BandControls {
  upTo: FormControl<string>;
  fixed: FormControl<string>;
  price: FormControl<string>;
}

export type BandGroup = FormGroup<BandControls>;
export type BandArray = FormArray<BandGroup>;

export interface DecimalRule {
  readonly decimals: number;
  readonly max: number;
  /** Verdadeiro quando zero não vale (limite de faixa, kg por m³, preço do kg). */
  readonly positive?: boolean;
}

/** Número em português, dentro da regra do campo; vazio, letras e casas demais dão o erro `decimal`. */
export function decimalField(rule: DecimalRule): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const value = parseDecimal(String(control.value ?? ''), rule.decimals);
    const valid = value !== null && value <= rule.max && (rule.positive !== true || value > 0);
    return valid ? null : { decimal: true };
  };
}

/** Cada limite é maior que o da faixa anterior. Limite vazio ou inválido fica com o erro do próprio campo. */
export const ascendingLimits: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  let previous = 0;
  for (const row of (control as BandArray).controls) {
    const limit = row.controls.upTo.enabled
      ? parseDecimal(row.controls.upTo.value, LIMIT_DECIMALS)
      : null;
    if (limit === null || limit <= 0) {
      continue;
    }
    if (limit <= previous) {
      return { ascending: true };
    }
    previous = limit;
  }
  return null;
};

export const limitRule: DecimalRule = { decimals: LIMIT_DECIMALS, max: LIMIT_MAX, positive: true };
export const fixedRule: DecimalRule = { decimals: FIXED_DECIMALS, max: FIXED_MAX };
export const priceRule: DecimalRule = { decimals: PRICE_DECIMALS, max: PRICE_MAX };

interface BandValues {
  readonly upTo: string;
  readonly fixed: string;
  readonly price: string;
}

function bandGroup(values: BandValues): BandGroup {
  return new FormGroup<BandControls>({
    upTo: new FormControl(values.upTo, {
      nonNullable: true,
      validators: decimalField(limitRule),
    }),
    fixed: new FormControl(values.fixed, {
      nonNullable: true,
      validators: decimalField(fixedRule),
    }),
    price: new FormControl(values.price, {
      nonNullable: true,
      validators: decimalField(priceRule),
    }),
  });
}

const BLANK_BAND: BandValues = { upTo: '', fixed: formatMoneyInput(0), price: '' };

function valuesOf(band: TariffBand): BandValues {
  return {
    upTo: band.upToCubicMeters === null ? '' : formatDecimalInput(band.upToCubicMeters),
    fixed: formatMoneyInput(band.fixedAmount),
    price: formatDecimalInput(band.pricePerCubicMeter, 2),
  };
}

/** As faixas de uma tarifa como campos de texto; sem tarifa, uma faixa só, em branco. */
export function bandArray(bands: readonly TariffBand[] | null): BandArray {
  const rows = bands === null ? [BLANK_BAND] : bands.map(valuesOf);
  const array: BandArray = new FormArray(rows.map(bandGroup), ascendingLimits);
  syncLimits(array);
  return array;
}

/** A última faixa nunca tem limite (o campo fica desligado, dizendo isso); as outras precisam de um. */
export function syncLimits(array: BandArray): void {
  array.controls.forEach((row, index) => {
    const upTo = row.controls.upTo;
    if (index === array.length - 1) {
      upTo.setValue(NO_LIMIT_TEXT);
      upTo.disable();
    } else if (upTo.disabled) {
      upTo.enable();
      upTo.setValue('');
    }
  });
}

export function addBand(array: BandArray): void {
  if (array.length >= MAX_BANDS) {
    return;
  }
  array.push(bandGroup(BLANK_BAND));
  syncLimits(array);
}

export function removeBand(array: BandArray, index: number): void {
  if (array.length <= 1) {
    return;
  }
  array.removeAt(index);
  syncLimits(array);
}

/** Faz de `target` uma cópia das faixas de `source`, com controles próprios. */
export function copyBands(target: BandArray, source: BandArray): void {
  target.clear();
  for (const row of source.controls) {
    const { upTo, fixed, price } = row.getRawValue();
    target.push(bandGroup({ upTo, fixed, price }));
  }
  syncLimits(target);
}

/** As faixas como números, a última sem limite. Nulo quando algum campo não é um número válido. */
export function toBands(array: BandArray): TariffBand[] | null {
  const bands: TariffBand[] = [];
  const rows = array.getRawValue();
  for (const [index, row] of rows.entries()) {
    const upTo = index === rows.length - 1 ? null : parseDecimal(row.upTo, LIMIT_DECIMALS);
    const fixed = parseDecimal(row.fixed, FIXED_DECIMALS);
    const price = parseDecimal(row.price, PRICE_DECIMALS);
    if (fixed === null || price === null || (index < rows.length - 1 && upTo === null)) {
      return null;
    }
    bands.push({ upToCubicMeters: upTo, fixedAmount: fixed, pricePerCubicMeter: price });
  }
  return bands;
}

/** O preço do m³ do gás em reais: kg por m³ vezes o preço do kg, arredondado para centavos (o meio centavo sobe). */
export function gasPricePerCubicMeter(kilosPerCubicMeter: number, pricePerKilo: number): number {
  const thousandthsOfKilo = Math.round(kilosPerCubicMeter * THOUSANDTHS);
  const cents = Math.round(pricePerKilo * CENTS);
  return Math.floor((thousandthsOfKilo * cents + HALF_THOUSANDTH) / THOUSANDTHS) / CENTS;
}
