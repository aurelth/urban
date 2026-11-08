import { ChangeDetectionStrategy, Component, Injector, inject, signal } from '@angular/core';
import {
  AbstractControl,
  NonNullableFormBuilder,
  ReactiveFormsModule,
  ValidationErrors,
  Validators,
} from '@angular/forms';
import { MatButton } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import {
  Expense,
  ExpenseGroup,
  ExpenseKind,
  ExpensePayload,
} from '../../core/finance/finance.models';
import { moneyField, parseMoney } from '../../core/finance/money';
import { addMonths, monthOptions } from '../../core/finance/month';
import { requiredText } from '../../shared/forms/required-text.validator';
import { SelectField, SelectOption } from '../../shared/ui/select-field/select.field';
import { TextField } from '../../shared/ui/text-field/text.field';
import { openFinanceDialog } from './finance.dialog';

const TEXT_MAX_LENGTH = 120;
const MIN_INSTALLMENTS = 2;
const MAX_INSTALLMENTS = 120;
const MONTHS_BEHIND = 36;
const MONTHS_AHEAD = 60;
const MIN_DUE_DAY = 1;
const MAX_DUE_DAY = 31;
const WHOLE_NUMBER = /^\d+$/;

const KINDS: readonly (SelectOption & { readonly hint: string })[] = [
  {
    value: 'fixed',
    label: 'Fixa mensal',
    hint: 'Mesmo valor todo mês, até o reajuste ou o encerramento',
  },
  {
    value: 'variable',
    label: 'Variável mensal',
    hint: 'O valor é informado a cada mês, como a conta de luz',
  },
  { value: 'single', label: 'Única', hint: 'Cobrada uma vez só, no mês escolhido' },
  {
    value: 'installment',
    label: 'Parcelada',
    hint: 'O valor total é dividido em parcelas mensais',
  },
];

const START_LABELS: Readonly<Record<ExpenseKind, string>> = {
  fixed: 'Começa em',
  variable: 'Começa em',
  single: 'Mês da despesa',
  installment: 'Mês da parcela',
};

const AMOUNT_LABELS: Readonly<Record<ExpenseKind, string>> = {
  fixed: 'Valor mensal (R$)',
  variable: 'Valor (R$)',
  single: 'Valor (R$)',
  installment: 'Valor total (R$)',
};

export interface ExpenseDialogConfig {
  /** A despesa que está sendo editada; nula quando é uma despesa nova. */
  readonly expense: Expense | null;
  readonly groups: readonly ExpenseGroup[];
  /** O mês de hoje ("AAAA-MM"), que serve de sugestão para o mês inicial. */
  readonly month: string;
  /** Grava a despesa. Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: (payload: ExpensePayload) => Promise<string | null>;
}

function wholeNumber(value: string): number | null {
  return WHOLE_NUMBER.test(value.trim()) ? Number(value.trim()) : null;
}

function installmentCountField(control: AbstractControl<string>): ValidationErrors | null {
  const count = wholeNumber(control.value);
  return count !== null && count >= MIN_INSTALLMENTS && count <= MAX_INSTALLMENTS
    ? null
    : { installmentCount: true };
}

function currentInstallmentField(control: AbstractControl<string>): ValidationErrors | null {
  const current = wholeNumber(control.value);
  const count = wholeNumber(control.parent?.get('installmentCount')?.value ?? '');
  return current !== null && current >= 1 && current <= (count ?? MAX_INSTALLMENTS)
    ? null
    : { currentInstallment: true };
}

function dueDayField(control: AbstractControl<string>): ValidationErrors | null {
  const day = wholeNumber(control.value);
  return day !== null && day >= MIN_DUE_DAY && day <= MAX_DUE_DAY ? null : { dueDay: true };
}

function endMonthField(control: AbstractControl<string>): ValidationErrors | null {
  const start: string = control.parent?.get('startMonth')?.value ?? '';
  return control.value !== '' && start !== '' && control.value < start
    ? { beforeStart: true }
    : null;
}

/** Cadastra uma despesa dos quatro tipos ou, na edição, muda só descrição, fornecedor, grupo e dia do vencimento. */
@Component({
  selector: 'urban-expense-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatIcon,
    SelectField,
    TextField,
  ],
  templateUrl: './expense.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExpenseDialog {
  protected readonly config = inject<ExpenseDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<ExpenseDialog, boolean>>(MatDialogRef);
  private readonly current = this.config.expense;
  protected readonly editing = this.current !== null;

  protected readonly kinds = KINDS;
  protected readonly groupOptions: SelectOption[] = this.config.groups.map((group) => ({
    value: group.id,
    label: group.name,
  }));
  protected readonly startOptions = monthOptions(
    addMonths(this.config.month, -MONTHS_BEHIND),
    addMonths(this.config.month, MONTHS_AHEAD),
  );

  protected readonly form = inject(NonNullableFormBuilder).group({
    kind: [(this.current?.kind ?? 'fixed') as ExpenseKind],
    groupId: [this.current?.groupId ?? this.config.groups[0]?.id ?? ''],
    description: [
      this.current?.description ?? '',
      [requiredText, Validators.maxLength(TEXT_MAX_LENGTH)],
    ],
    supplier: [this.current?.supplier ?? '', Validators.maxLength(TEXT_MAX_LENGTH)],
    startMonth: [this.current?.startMonth ?? this.config.month],
    endMonth: ['', endMonthField],
    amount: ['', moneyField],
    installmentCount: ['', installmentCountField],
    currentInstallment: ['1', currentInstallmentField],
    dueDay: [this.current === null ? '' : String(this.current.dueDay), dueDayField],
  });

  protected readonly kind = signal<ExpenseKind>(this.form.controls.kind.value);
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly changed = signal(false);

  protected readonly descriptionErrors = {
    required: 'Informe a descrição da despesa',
    maxlength: 'A descrição pode ter até 120 caracteres',
  };
  protected readonly supplierErrors = { maxlength: 'O fornecedor pode ter até 120 caracteres' };
  protected readonly amountErrors = {
    money: 'Informe um valor maior que zero, com até 2 casas decimais',
  };
  protected readonly countErrors = { installmentCount: 'Informe de 2 a 120 parcelas' };
  protected readonly currentErrors = {
    currentInstallment: 'Informe uma parcela entre 1 e o total de parcelas',
  };
  protected readonly dueDayErrors = { dueDay: 'Informe o dia do vencimento, de 1 a 31' };
  protected readonly endErrors = {
    beforeStart: 'O mês final não pode ser anterior ao mês inicial',
  };

  constructor() {
    const controls = this.form.controls;
    this.applyKind(controls.kind.value);

    controls.kind.valueChanges.subscribe((kind) => {
      this.kind.set(kind);
      this.applyKind(kind);
    });
    controls.startMonth.valueChanges.subscribe(() => controls.endMonth.updateValueAndValidity());
    controls.installmentCount.valueChanges.subscribe(() =>
      controls.currentInstallment.updateValueAndValidity(),
    );
    this.form.valueChanges.subscribe(() => this.changed.set(this.differsFromCurrent()));
  }

  protected kindHint(): string {
    return KINDS.find((option) => option.value === this.kind())?.hint ?? '';
  }

  protected dueDayHint(): string {
    return this.editing
      ? 'De 1 a 31. Vale para os próximos lançamentos; os que já existem não mudam'
      : 'De 1 a 31. Nos meses mais curtos vale o último dia';
  }

  protected startLabel(): string {
    return START_LABELS[this.kind()];
  }

  protected amountLabel(): string {
    return AMOUNT_LABELS[this.kind()];
  }

  protected async confirm(): Promise<void> {
    if (this.busy() || (this.editing && !this.changed())) {
      return;
    }
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    const message = await this.config.save(this.payload());
    this.busy.set(false);

    if (message === null) {
      this.ref.close(true);
      return;
    }
    this.error.set(message);
  }

  /** Só os campos do tipo escolhido participam do formulário; na edição, só descrição, fornecedor e grupo. */
  private applyKind(kind: ExpenseKind): void {
    const controls = this.form.controls;
    const creating = !this.editing;
    this.toggle(controls.kind, creating);
    this.toggle(controls.startMonth, creating);
    this.toggle(controls.endMonth, creating && (kind === 'fixed' || kind === 'variable'));
    this.toggle(controls.amount, creating && kind !== 'variable');
    this.toggle(controls.installmentCount, creating && kind === 'installment');
    this.toggle(controls.currentInstallment, creating && kind === 'installment');
  }

  private toggle(control: AbstractControl, enabled: boolean): void {
    if (enabled && control.disabled) {
      control.enable({ emitEvent: false });
    } else if (!enabled && control.enabled) {
      control.disable({ emitEvent: false });
    }
  }

  private differsFromCurrent(): boolean {
    const value = this.form.getRawValue();
    return (
      value.description.trim() !== (this.current?.description ?? '') ||
      value.supplier.trim() !== (this.current?.supplier ?? '') ||
      value.groupId !== (this.current?.groupId ?? '') ||
      wholeNumber(value.dueDay) !== (this.current?.dueDay ?? null)
    );
  }

  private payload(): ExpensePayload {
    const value = this.form.getRawValue();
    const supplier = value.supplier.trim() === '' ? null : value.supplier.trim();
    const common = {
      groupId: value.groupId,
      description: value.description.trim(),
      supplier,
      dueDay: wholeNumber(value.dueDay) ?? 0,
    };

    if (this.current !== null) {
      return {
        ...common,
        kind: this.current.kind,
        startMonth: this.current.startMonth,
        endMonth: this.current.endMonth,
        amount: this.current.currentAmount,
        installmentCount: this.current.installmentCount,
        currentInstallment: this.current.currentInstallment,
      };
    }

    const kind = value.kind;
    return {
      ...common,
      kind,
      startMonth: value.startMonth,
      endMonth: kind === 'fixed' || kind === 'variable' ? value.endMonth || null : null,
      amount: kind === 'variable' ? null : parseMoney(value.amount),
      installmentCount: kind === 'installment' ? wholeNumber(value.installmentCount) : null,
      currentInstallment: kind === 'installment' ? wholeNumber(value.currentInstallment) : null,
    };
  }
}

export function openExpenseDialog(
  injector: Injector,
  config: ExpenseDialogConfig,
): Promise<boolean> {
  return openFinanceDialog(injector, ExpenseDialog, config);
}
