import { ChangeDetectionStrategy, Component, Injector, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { openFinanceDialog } from './finance.dialog';
import { monthName } from './tariff-values';

export interface DeleteTariffDialogConfig {
  /** O mês em que a tarifa começa, "AAAA-MM". */
  readonly month: string;
  /** Apaga a tarifa. Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: () => Promise<string | null>;
}

/** Confirma que a tarifa de um mês vai ser apagada, lembrando que os meses já fechados guardam o que cobraram. */
@Component({
  selector: 'urban-delete-tariff-dialog',
  imports: [MatButton, MatDialogActions, MatDialogContent, MatDialogTitle, MatIcon],
  templateUrl: './delete-tariff.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeleteTariffDialog {
  protected readonly config = inject<DeleteTariffDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<DeleteTariffDialog, boolean>>(MatDialogRef);
  protected readonly name = monthName(this.config.month);

  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected async confirm(): Promise<void> {
    if (this.busy()) {
      return;
    }
    this.busy.set(true);
    this.error.set(null);
    const message = await this.config.save();
    this.busy.set(false);

    if (message === null) {
      this.ref.close(true);
      return;
    }
    this.error.set(message);
  }
}

export function openDeleteTariffDialog(
  injector: Injector,
  config: DeleteTariffDialogConfig,
): Promise<boolean> {
  return openFinanceDialog(injector, DeleteTariffDialog, config);
}
