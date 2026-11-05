import {
  ChangeDetectionStrategy,
  Component,
  OnInit,
  computed,
  inject,
  signal,
} from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatCard, MatCardContent } from '@angular/material/card';
import { MatIcon } from '@angular/material/icon';
import { MatRadioButton, MatRadioGroup } from '@angular/material/radio';
import { RouterLink } from '@angular/router';
import { firstValueFrom } from 'rxjs';
import { describeProblem } from '../../core/http/problem-message';
import { UnitsApi } from '../../core/units/units.api';
import { UnitKind } from '../../core/units/units.models';
import { TextField } from '../../shared/ui/text-field/text.field';
import {
  BatchRow,
  FloorPlan,
  MAX_BATCH_UNITS,
  buildFloors,
  generateFloorRows,
  generateHouseRows,
  totalUnits,
} from './unit-batch.generator';
import {
  RowProblems,
  findDuplicateRows,
  splitRemainingFraction,
  toBatchPayload,
  validateRow,
} from './unit-batch.rows';
import { formatPercent } from './unit-values';
import { BATCH_MESSAGES, BATCH_REJECTED_MESSAGE } from './units.messages';
import { UnitsBatchStore } from './units-batch.store';

type RowField = 'number' | 'block' | 'fraction' | 'area';

interface RowView {
  readonly row: BatchRow;
  readonly messages: readonly string[];
  readonly invalid: ReadonlySet<RowField>;
}

const DUPLICATE_MESSAGE = 'Este bloco e número aparecem mais de uma vez no lote';
const LIMIT_MESSAGE = `O lote aceita até ${MAX_BATCH_UNITS} unidades por vez`;
const EMPTY_PLAN_MESSAGE = 'Informe ao menos uma unidade';
const BLOCK_MAX_LENGTH = 30;
const MAX_FLOOR = 99;

const ISSUE_FIELDS: Readonly<Record<string, readonly RowField[]>> = {
  'unit.number.invalid': ['number'],
  'unit.block.invalid': ['block'],
  'unit.ideal_fraction.invalid': ['fraction'],
  'unit.ideal_fraction.sum_exceeded': ['fraction'],
  'unit.area.invalid': ['area'],
  'unit.identifier.already_exists': ['number', 'block'],
};

/**
 * Cadastro em lote. O gerador monta os apartamentos andar por andar (cada andar pode ter uma quantidade diferente) ou
 * as casas em sequência e acrescenta as linhas à pré-visualização, onde tudo pode ser corrigido antes de enviar. O
 * lote é gravado inteiro ou não é gravado: se a API recusar, as linhas com problema ficam marcadas.
 */
@Component({
  selector: 'urban-units-batch-page',
  imports: [
    ReactiveFormsModule,
    RouterLink,
    MatButton,
    MatIconButton,
    MatCard,
    MatCardContent,
    MatIcon,
    MatRadioButton,
    MatRadioGroup,
    TextField,
  ],
  templateUrl: './units-batch.page.html',
  providers: [UnitsBatchStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UnitsBatchPage implements OnInit {
  protected readonly store = inject(UnitsBatchStore);
  private readonly api = inject(UnitsApi);

  protected readonly generator = inject(NonNullableFormBuilder).group({
    kind: ['apartment' as UnitKind],
    block: ['', Validators.maxLength(BLOCK_MAX_LENGTH)],
    firstFloor: ['1', floorField()],
    lastFloor: ['', [floorField(), notBeforeFirstFloor]],
    perFloor: ['', wholeField('perFloor', 1, MAX_FLOOR)],
    houseCount: ['', wholeField('count', 1, MAX_BATCH_UNITS)],
  });

  protected readonly rows = signal<readonly BatchRow[]>([]);
  protected readonly floors = signal<readonly FloorPlan[] | null>(null);
  protected readonly generatorError = signal<string | null>(null);
  protected readonly submitError = signal<string | null>(null);
  protected readonly kind = signal<UnitKind>('apartment');

  /** Fração ideal já ocupada pelas unidades cadastradas; nula enquanto não chega (ou se a busca falhar). */
  private readonly used = signal<number | null>(null);
  private readonly rejectedWithRows = signal(false);

  protected readonly blockErrors = { maxlength: 'O bloco pode ter até 30 caracteres' };
  protected readonly firstFloorErrors = { floor: 'Informe o primeiro andar, de 0 a 99' };
  protected readonly lastFloorErrors = {
    floor: 'Informe o último andar, de 0 a 99',
    order: 'O último andar não pode ser menor que o primeiro',
  };
  protected readonly perFloorErrors = { perFloor: 'Informe de 1 a 99 unidades por andar' };
  protected readonly houseCountErrors = { count: `Informe de 1 a ${MAX_BATCH_UNITS} casas` };

  protected readonly views = computed<RowView[]>(() => {
    const rows = this.rows();
    const duplicates = findDuplicateRows(rows);
    const issues = this.store.issues();
    return rows.map((row, position) => {
      const problems = validateRow(row);
      const messages = Object.values(problems);
      const invalid = new Set<RowField>(Object.keys(problems) as RowField[]);
      if (duplicates.has(position)) {
        messages.push(DUPLICATE_MESSAGE);
        invalid.add('number').add('block');
      }
      for (const issue of issues.filter((candidate) => candidate.position === position)) {
        messages.push(
          describeProblem(
            { status: 0, code: issue.code, detail: null, retryAfterSeconds: null },
            BATCH_MESSAGES,
          ),
        );
        (ISSUE_FIELDS[issue.code] ?? ['number']).forEach((field) => invalid.add(field));
      }
      return { row, messages, invalid };
    });
  });

  protected readonly hasProblems = computed(() => {
    const rows = this.rows();
    const duplicates = findDuplicateRows(rows);
    return rows.some((row, position) => duplicates.has(position) || hasAny(validateRow(row)));
  });

  protected readonly canSubmit = computed(
    () => this.rows().length > 0 && !this.hasProblems() && !this.store.saving(),
  );
  protected readonly submitLabel = computed(() => `Cadastrar ${countOf(this.rows().length)}`);
  protected readonly rowCountText = computed(() => `${countOf(this.rows().length)} no lote`);
  protected readonly floorTotal = computed(() => countOf(totalUnits(this.floors() ?? [])));
  protected readonly canSplit = computed(
    () =>
      this.used() !== null &&
      !this.store.saving() &&
      this.rows().some((row) => row.fraction.trim() === ''),
  );
  protected readonly usedText = computed(() => {
    const used = this.used();
    return used === null ? null : formatPercent(used);
  });
  protected readonly showSubmitError = computed(
    () =>
      this.submitError() !== null && (!this.rejectedWithRows() || this.store.issues().length > 0),
  );

  ngOnInit(): void {
    this.generator.controls.firstFloor.valueChanges.subscribe(() => {
      this.generator.controls.lastFloor.updateValueAndValidity({ emitEvent: false });
    });
    this.generator.controls.kind.valueChanges.subscribe((kind) => this.kind.set(kind));
    void this.loadUsedFraction();
  }

  protected buildFloors(): void {
    const { firstFloor, lastFloor, perFloor } = this.generator.controls;
    const controls = [firstFloor, lastFloor, perFloor];
    controls.forEach((control) => control.markAsTouched());
    if (controls.some((control) => control.invalid)) {
      this.floors.set(null);
      return;
    }
    this.generatorError.set(null);
    this.floors.set(
      buildFloors(Number(firstFloor.value), Number(lastFloor.value), Number(perFloor.value)),
    );
  }

  protected setFloorCount(floor: number, event: Event): void {
    const text = (event.target as HTMLInputElement).value.trim();
    const count = /^\d{1,2}$/.test(text) ? Number(text) : 0;
    this.floors.update((plans) =>
      plans === null
        ? plans
        : plans.map((plan) => (plan.floor === floor ? { ...plan, count } : plan)),
    );
  }

  protected generate(): void {
    const { block, houseCount } = this.generator.controls;
    block.markAsTouched();
    houseCount.markAsTouched();
    if (block.invalid) {
      return;
    }
    let generated: BatchRow[];
    if (this.kind() === 'house') {
      if (houseCount.invalid) {
        return;
      }
      generated = generateHouseRows(block.value, Number(houseCount.value));
    } else {
      generated = generateFloorRows(block.value, this.floors() ?? []);
    }
    if (generated.length === 0) {
      this.generatorError.set(EMPTY_PLAN_MESSAGE);
      return;
    }
    if (this.rows().length + generated.length > MAX_BATCH_UNITS) {
      this.generatorError.set(LIMIT_MESSAGE);
      return;
    }
    this.generatorError.set(null);
    this.floors.set(null);
    this.rows.update((rows) => [...rows, ...generated]);
  }

  protected addRow(): void {
    const last = this.rows().at(-1);
    this.rows.update((rows) => [
      ...rows,
      {
        block: last?.block ?? '',
        number: '',
        type: last?.type ?? 'apartment',
        fraction: '',
        area: '',
      },
    ]);
  }

  protected editRow(position: number, field: keyof BatchRow, event: Event): void {
    const value = (event.target as HTMLInputElement | HTMLSelectElement).value;
    this.rows.update((rows) =>
      rows.map((row, index) => (index === position ? { ...row, [field]: value } : row)),
    );
    this.store.clearIssue(position);
  }

  /** Tirar uma linha muda a posição das seguintes, então as marcas da API deixam de valer. */
  protected removeRow(position: number): void {
    this.rows.update((rows) => rows.filter((_, index) => index !== position));
    this.store.clearIssues();
    this.submitError.set(null);
  }

  protected clearAll(): void {
    this.rows.set([]);
    this.store.clearIssues();
    this.submitError.set(null);
  }

  protected splitFraction(): void {
    const used = this.used();
    if (used !== null) {
      this.rows.update((rows) => splitRemainingFraction(rows, used));
    }
  }

  protected async submit(): Promise<void> {
    if (!this.canSubmit()) {
      return;
    }
    this.submitError.set(null);
    const problem = await this.store.submit(toBatchPayload(this.rows()));
    if (problem === null) {
      return;
    }
    const withRows = this.store.issues().length > 0;
    this.rejectedWithRows.set(withRows);
    this.submitError.set(
      withRows ? BATCH_REJECTED_MESSAGE : describeProblem(problem, BATCH_MESSAGES),
    );
  }

  protected startOver(): void {
    this.store.startOver();
    this.rows.set([]);
    this.floors.set(null);
    this.submitError.set(null);
    this.generatorError.set(null);
  }

  private async loadUsedFraction(): Promise<void> {
    try {
      const summary = await firstValueFrom(this.api.summary());
      this.used.set(summary.idealFractionTotal);
    } catch {
      this.used.set(null);
    }
  }
}

function countOf(total: number): string {
  return `${total} ${total === 1 ? 'unidade' : 'unidades'}`;
}

function hasAny(problems: RowProblems): boolean {
  return Object.keys(problems).length > 0;
}

function parseWhole(text: string): number | null {
  const trimmed = text.trim();
  return /^\d+$/.test(trimmed) ? Number(trimmed) : null;
}

function floorField() {
  return wholeField('floor', 0, MAX_FLOOR);
}

function wholeField(key: string, minimum: number, maximum: number) {
  return (control: AbstractControl<string>): ValidationErrors | null => {
    const value = parseWhole(control.value);
    return value !== null && value >= minimum && value <= maximum ? null : { [key]: true };
  };
}

/** O último andar não pode vir antes do primeiro; só se compara quando os dois são números válidos. */
function notBeforeFirstFloor(control: AbstractControl<string>): ValidationErrors | null {
  const first = parseWhole(control.parent?.get('firstFloor')?.value ?? '');
  const last = parseWhole(control.value);
  return first !== null && last !== null && last < first ? { order: true } : null;
}
