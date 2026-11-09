import {
  AbstractControl,
  FormArray,
  FormControl,
  FormGroup,
  ValidationErrors,
  ValidatorFn,
} from '@angular/forms';
import { formatDecimalInput, parseDecimal } from '../../core/finance/decimal';
import { MonthReadings, ReadingInput, UnitReadings } from '../../core/finance/finance.models';
import { showsGas } from './readings-values';

/** A leitura de um medidor vai de 0 a 9.999.999,999 m³, com até 3 casas decimais (como a API). */
export const MAX_READING = 9_999_999.999;
const READING_DECIMALS = 3;

/** Os quatro campos de uma unidade, como a pessoa os digita. */
export interface ReadingTexts {
  readonly waterPrevious: string;
  readonly waterCurrent: string;
  readonly gasPrevious: string;
  readonly gasCurrent: string;
}

export type ReadingRow = FormGroup<{
  waterPrevious: FormControl<string>;
  waterCurrent: FormControl<string>;
  gasPrevious: FormControl<string>;
  gasCurrent: FormControl<string>;
}>;

export type ReadingsForm = FormGroup<{ units: FormArray<ReadingRow> }>;

/** O que cada campo mostra ao abrir: a leitura gravada, ou a sugestão do mês passado (ou zero) como anterior e nada como atual. */
export function readingTexts(unit: UnitReadings): ReadingTexts {
  return {
    waterPrevious: formatDecimalInput(unit.water?.previous ?? unit.suggestedWaterPrevious ?? 0),
    waterCurrent: unit.water ? formatDecimalInput(unit.water.current) : '',
    gasPrevious: formatDecimalInput(unit.gas?.previous ?? unit.suggestedGasPrevious ?? 0),
    gasCurrent: unit.gas ? formatDecimalInput(unit.gas.current) : '',
  };
}

/** Leitura em m³: a anterior é obrigatória; a atual pode ficar vazia (sem leitura no mês). Erro: `decimal`. */
export function readingField(required: boolean): ValidatorFn {
  return (control: AbstractControl): ValidationErrors | null => {
    const text = String(control.value).trim();
    if (text === '') {
      return required ? { decimal: true } : null;
    }
    const value = parseDecimal(text, READING_DECIMALS);
    return value === null || value > MAX_READING ? { decimal: true } : null;
  };
}

/** A atual não pode ser menor que a anterior. Só compara quando as duas são números; erros: `waterOrder` e `gasOrder`. */
const inOrder: ValidatorFn = (control: AbstractControl): ValidationErrors | null => {
  const row = control as ReadingRow;
  const errors: ValidationErrors = {};
  if (isBelow(row.controls.waterPrevious.value, row.controls.waterCurrent.value)) {
    errors['waterOrder'] = true;
  }
  if (isBelow(row.controls.gasPrevious.value, row.controls.gasCurrent.value)) {
    errors['gasOrder'] = true;
  }
  return Object.keys(errors).length === 0 ? null : errors;
};

function isBelow(previous: string, current: string): boolean {
  const before = parseDecimal(previous, READING_DECIMALS);
  const now = parseDecimal(current, READING_DECIMALS);
  return before !== null && now !== null && now < before;
}

function textControl(value: string, required: boolean): FormControl<string> {
  return new FormControl(value, { nonNullable: true, validators: readingField(required) });
}

function readingRow(unit: UnitReadings): ReadingRow {
  const texts = readingTexts(unit);
  return new FormGroup(
    {
      waterPrevious: textControl(texts.waterPrevious, true),
      waterCurrent: textControl(texts.waterCurrent, false),
      gasPrevious: textControl(texts.gasPrevious, true),
      gasCurrent: textControl(texts.gasCurrent, false),
    },
    { validators: inOrder },
  );
}

/** O formulário do mês: uma linha por unidade, na ordem da API. Fechado, todos os campos ficam travados. */
export function readingsForm(data: MonthReadings): ReadingsForm {
  const form = new FormGroup({ units: new FormArray<ReadingRow>([]) });
  fillReadingsForm(form, data);
  return form;
}

/**
 * Mostra o mês recebido nas linhas do formulário, sem avisar quem escuta e voltando o formulário a "intocado". Reaproveita
 * as linhas que já existem (o foco e a tela ficam como estão) e só cria ou tira as que sobram ou faltam.
 */
export function fillReadingsForm(form: ReadingsForm, data: MonthReadings): void {
  const rows = form.controls.units;
  data.units.forEach((unit, index) => {
    if (index < rows.length) {
      rows.at(index).setValue(readingTexts(unit), { emitEvent: false });
    } else {
      rows.push(readingRow(unit), { emitEvent: false });
    }
  });
  while (rows.length > data.units.length) {
    rows.removeAt(rows.length - 1, { emitEvent: false });
  }
  if (data.isClosed) {
    form.disable({ emitEvent: false });
  } else {
    form.enable({ emitEvent: false });
  }
  form.markAsPristine();
  form.markAsUntouched();
  form.updateValueAndValidity({ emitEvent: false });
}

/** Se algum campo mostra algo diferente do que o mês traz (mesmo um texto ainda inválido). As linhas seguem a ordem das unidades. */
export function hasChanges(data: MonthReadings, rows: readonly ReadingTexts[]): boolean {
  return data.units.some((unit, index) => {
    const initial = readingTexts(unit);
    const now = rows[index];
    return (
      now.waterPrevious !== initial.waterPrevious ||
      now.waterCurrent !== initial.waterCurrent ||
      (showsGas(data) &&
        (now.gasPrevious !== initial.gasPrevious || now.gasCurrent !== initial.gasCurrent))
    );
  });
}

/**
 * O que mudou desde que o mês foi lido: leituras novas ou corrigidas (sempre com a anterior) e as apagadas (atual nulo,
 * que a API entende como retirar). O gás só entra quando o mês o mostra. As linhas estão na ordem das unidades.
 */
export function changedReadings(
  data: MonthReadings,
  rows: readonly ReadingTexts[],
): readonly ReadingInput[] {
  const withGas = showsGas(data);
  return data.units.flatMap((unit, index): ReadingInput[] => {
    const initial = readingTexts(unit);
    const now = rows[index];
    return [
      change(
        unit.unitId,
        'water',
        initial.waterPrevious,
        initial.waterCurrent,
        now.waterPrevious,
        now.waterCurrent,
      ),
      ...(withGas
        ? [
            change(
              unit.unitId,
              'gas',
              initial.gasPrevious,
              initial.gasCurrent,
              now.gasPrevious,
              now.gasCurrent,
            ),
          ]
        : []),
    ].filter((reading): reading is ReadingInput => reading !== null);
  });
}

function change(
  unitId: string,
  service: 'water' | 'gas',
  initialPrevious: string,
  initialCurrent: string,
  previous: string,
  current: string,
): ReadingInput | null {
  const typed = current.trim();
  if (typed === '') {
    return initialCurrent === '' ? null : { unitId, service, current: null };
  }
  if (previous === initialPrevious && current === initialCurrent) {
    return null;
  }
  const before = parseDecimal(previous, READING_DECIMALS);
  const now = parseDecimal(typed, READING_DECIMALS);
  return before === null || now === null
    ? null
    : { unitId, service, previous: before, current: now };
}

const consumptionFormat = new Intl.NumberFormat('pt-BR', {
  maximumFractionDigits: READING_DECIMALS,
});

/** "12,5 m³" quando as duas leituras são válidas e a atual não é menor; nulo caso contrário. */
export function consumptionText(previous: string, current: string): string | null {
  const before = parseDecimal(previous, READING_DECIMALS);
  const now = parseDecimal(current, READING_DECIMALS);
  return before === null || now === null || now < before
    ? null
    : `${consumptionFormat.format(now - before)} m³`;
}
