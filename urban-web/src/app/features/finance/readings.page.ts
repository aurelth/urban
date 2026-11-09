import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  OnInit,
  computed,
  effect,
  inject,
  signal,
  untracked,
} from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ReactiveFormsModule } from '@angular/forms';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { ActivatedRoute, RouterLink } from '@angular/router';
import { NOW } from '../../core/finance/clock';
import { UnitReadings } from '../../core/finance/finance.models';
import { addMonths, isMonthInWindow, monthTitle } from '../../core/finance/month';
import { describeProblem } from '../../core/http/problem-message';
import { focusFirstInvalidInput } from '../../shared/forms/focus-first-invalid';
import { TextField } from '../../shared/ui/text-field/text.field';
import { unitLabel } from '../units/unit-values';
import { FINANCE_ACTION_MESSAGES } from './finance.messages';
import {
  ReadingRow,
  ReadingTexts,
  changedReadings,
  consumptionText,
  fillReadingsForm,
  hasChanges,
  readingsForm,
} from './readings.form';
import { ReadingsStore } from './readings.store';
import { coverageLines, initialMonth, showsGas } from './readings-values';

type ServiceKey = 'water' | 'gas';

interface ServiceSection {
  readonly key: ServiceKey;
  readonly label: string;
}

const WATER: ServiceSection = { key: 'water', label: 'Água' };
const GAS: ServiceSection = { key: 'gas', label: 'Gás' };

const NOTHING_TO_SEND =
  'Informe a leitura atual das unidades alteradas. Sem ela, a leitura anterior não é salva';

/**
 * Leituras de água e gás de um mês: uma linha por unidade, com a leitura anterior (a atual do mês passado, que pode ser
 * corrigida) e a atual. Grava tudo de uma vez; a API aceita o pedido inteiro ou recusa inteiro. Mês fechado fica só para
 * leitura. Enquanto há leituras não salvas, o mês não troca, para ninguém perder o que digitou.
 */
@Component({
  selector: 'urban-readings-page',
  imports: [ReactiveFormsModule, RouterLink, MatButton, MatIconButton, MatIcon, TextField],
  templateUrl: './readings.page.html',
  providers: [ReadingsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReadingsPage implements OnInit {
  protected readonly store = inject(ReadingsStore);
  private readonly host = inject<ElementRef<HTMLElement>>(ElementRef);
  private readonly now = inject(NOW);
  private readonly route = inject(ActivatedRoute);
  private readonly destroyRef = inject(DestroyRef);

  protected readonly form = readingsForm({
    month: '',
    isClosed: false,
    hasTariff: false,
    hasGas: false,
    units: [],
  });
  protected readonly rows = signal<readonly ReadingRow[]>([]);
  private readonly values = signal<readonly ReadingTexts[]>([]);

  protected readonly month = signal(
    initialMonth(this.route.snapshot.queryParamMap.get('mes'), this.now()),
  );
  protected readonly notice = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);

  protected readonly dirty = computed(() => {
    const data = this.store.data();
    return data !== null && hasChanges(data, this.values());
  });
  protected readonly sections = computed<readonly ServiceSection[]>(() => {
    const data = this.store.data();
    return data !== null && showsGas(data) ? [WATER, GAS] : [WATER];
  });
  protected readonly coverage = computed(() => {
    const data = this.store.data();
    return data === null ? [] : coverageLines(data);
  });

  protected readonly title = monthTitle;
  protected readonly unitLabel = unitLabel;
  protected readonly previousErrors = {
    decimal: 'Informe a leitura anterior em m³, com até 3 casas decimais',
  };
  protected readonly currentErrors = {
    decimal: 'Informe a leitura atual em m³, com até 3 casas decimais',
  };

  constructor() {
    // Quando o mês chega (ao abrir, trocar de mês ou gravar), os campos passam a mostrar o que a API devolveu.
    effect(() => {
      const data = this.store.data();
      if (data !== null) {
        untracked(() => {
          fillReadingsForm(this.form, data);
          this.rows.set([...this.form.controls.units.controls]);
          this.values.set(this.form.getRawValue().units);
        });
      }
    });
    this.form.valueChanges.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      this.values.set(this.form.getRawValue().units);
      this.clearMessages();
    });
  }

  ngOnInit(): void {
    void this.show(this.month());
  }

  protected canGo(delta: number): boolean {
    return isMonthInWindow(addMonths(this.month(), delta), this.now());
  }

  protected go(delta: number): Promise<void> {
    return this.show(addMonths(this.month(), delta));
  }

  protected reload(): Promise<void> {
    return this.show(this.month());
  }

  protected unit(index: number): UnitReadings | null {
    return this.store.data()?.units[index] ?? null;
  }

  protected consumption(index: number, key: ServiceKey): string | null {
    const row = this.values()[index];
    return key === 'water'
      ? consumptionText(row.waterPrevious, row.waterCurrent)
      : consumptionText(row.gasPrevious, row.gasCurrent);
  }

  protected hasOrderError(row: ReadingRow, key: ServiceKey): boolean {
    return row.hasError(`${key}Order`);
  }

  protected async save(): Promise<void> {
    const data = this.store.data();
    if (data === null || this.store.busy()) {
      return;
    }
    this.clearMessages();
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      focusFirstInvalidInput(this.host.nativeElement);
      return;
    }
    const readings = changedReadings(data, this.form.getRawValue().units);
    if (readings.length === 0) {
      this.actionError.set(NOTHING_TO_SEND);
      return;
    }
    const problem = await this.store.save(readings);
    if (problem === null) {
      this.notice.set('Leituras salvas');
    } else {
      this.actionError.set(describeProblem(problem, FINANCE_ACTION_MESSAGES));
    }
  }

  protected discard(): void {
    const data = this.store.data();
    if (data === null) {
      return;
    }
    this.clearMessages();
    fillReadingsForm(this.form, data);
    this.rows.set([...this.form.controls.units.controls]);
    this.values.set(this.form.getRawValue().units);
  }

  private async show(month: string): Promise<void> {
    this.clearMessages();
    this.month.set(month);
    await this.store.show(month);
  }

  private clearMessages(): void {
    this.notice.set(null);
    this.actionError.set(null);
  }
}
