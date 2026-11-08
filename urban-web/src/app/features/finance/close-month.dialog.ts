import { ChangeDetectionStrategy, Component, Injector, inject, signal } from '@angular/core';
import { AbstractControl, NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { formatMoney } from '../../core/finance/money';
import { formatDate, monthTitle, parseDueDate, suggestedDueDate } from '../../core/finance/month';
import { TextField } from '../../shared/ui/text-field/text.field';
import { openFinanceDialog } from './finance.dialog';

export interface CloseMonthDialogConfig {
  /** O mês que vai fechar, "AAAA-MM". */
  readonly month: string;
  /** O total do mês, que será dividido entre as unidades. */
  readonly total: number;
  /** Fecha o mês com o vencimento ("AAAA-MM-DD"). Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: (dueDate: string) => Promise<string | null>;
}

/**
 * Pergunta o vencimento das cobranças e fecha o mês. Já sugere o dia 10 do mês seguinte; a síndica pode trocar.
 * A data é digitada como dd/mm/aaaa e precisa ser real e não anterior ao primeiro dia do mês.
 */
@Component({
  selector: 'urban-close-month-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatIcon,
    TextField,
  ],
  templateUrl: './close-month.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class CloseMonthDialog {
  protected readonly config = inject<CloseMonthDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<CloseMonthDialog, boolean>>(MatDialogRef);
  protected readonly title = monthTitle(this.config.month);
  protected readonly total = formatMoney(this.config.total);

  protected readonly form = inject(NonNullableFormBuilder).group({
    dueDate: [
      suggestedDueDate(this.config.month),
      (control: AbstractControl<string>) =>
        parseDueDate(control.value, this.config.month) === null ? { dueDate: true } : null,
    ],
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly dueDateErrors = {
    dueDate: `Informe uma data válida, a partir de ${formatDate(`${this.config.month}-01`)}`,
  };

  protected async confirm(): Promise<void> {
    if (this.busy()) {
      return;
    }
    const dueDate = parseDueDate(this.form.getRawValue().dueDate, this.config.month);
    if (this.form.invalid || dueDate === null) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    const message = await this.config.save(dueDate);
    this.busy.set(false);

    if (message === null) {
      this.ref.close(true);
      return;
    }
    this.error.set(message);
  }
}

export function openCloseMonthDialog(
  injector: Injector,
  config: CloseMonthDialogConfig,
): Promise<boolean> {
  return openFinanceDialog(injector, CloseMonthDialog, config);
}
