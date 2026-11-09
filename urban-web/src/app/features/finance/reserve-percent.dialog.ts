import { ChangeDetectionStrategy, Component, Injector, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule } from '@angular/forms';
import { MatButton } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { formatDecimalInput, parseDecimal } from '../../core/finance/decimal';
import { TextField } from '../../shared/ui/text-field/text.field';
import { openFinanceDialog } from './finance.dialog';
import { decimalField } from './tariff.form';

const DECIMALS = 2;
const MAX_PERCENT = 100;

export interface ReservePercentDialogConfig {
  /** O percentual de hoje, de 0 a 100. */
  readonly percent: number;
  /** Grava o percentual. Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: (percent: number) => Promise<string | null>;
}

/** Muda o percentual do fundo de reserva. O botão só habilita depois de mudar o valor. */
@Component({
  selector: 'urban-reserve-percent-dialog',
  imports: [
    ReactiveFormsModule,
    MatButton,
    MatDialogActions,
    MatDialogContent,
    MatDialogTitle,
    MatIcon,
    TextField,
  ],
  templateUrl: './reserve-percent.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ReservePercentDialog {
  protected readonly config = inject<ReservePercentDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<ReservePercentDialog, boolean>>(MatDialogRef);

  private readonly current = formatDecimalInput(this.config.percent);

  protected readonly form = inject(NonNullableFormBuilder).group({
    percent: [this.current, [decimalField({ decimals: DECIMALS, max: MAX_PERCENT })]],
  });

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly changed = signal(false);

  protected readonly percentErrors = {
    decimal: 'Informe um percentual de 0 a 100, com até 2 casas',
  };

  constructor() {
    this.form.controls.percent.valueChanges.subscribe((text) => {
      this.changed.set(text.trim() !== this.current);
    });
  }

  protected async confirm(): Promise<void> {
    if (this.busy() || !this.changed()) {
      return;
    }
    const percent = parseDecimal(this.form.getRawValue().percent, DECIMALS);
    if (this.form.invalid || percent === null) {
      this.form.markAllAsTouched();
      return;
    }

    this.busy.set(true);
    this.error.set(null);
    const message = await this.config.save(percent);
    this.busy.set(false);

    if (message === null) {
      this.ref.close(true);
      return;
    }
    this.error.set(message);
  }
}

export function openReservePercentDialog(
  injector: Injector,
  config: ReservePercentDialogConfig,
): Promise<boolean> {
  return openFinanceDialog(injector, ReservePercentDialog, config);
}
