import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  Signal,
  computed,
  inject,
} from '@angular/core';
import { MatButton } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogActions,
  MatDialogClose,
  MatDialogContent,
  MatDialogTitle,
} from '@angular/material/dialog';

interface IdleWarningData {
  secondsLeft: Signal<number>;
}

/** Aviso de que a sessão está para acabar por falta de atividade. */
@Component({
  selector: 'urban-idle-warning-dialog',
  imports: [MatButton, MatDialogActions, MatDialogClose, MatDialogContent, MatDialogTitle],
  templateUrl: './idle-warning.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class IdleWarningDialog {
  private readonly secondsLeft = inject<IdleWarningData>(MAT_DIALOG_DATA).secondsLeft;

  protected readonly countdown = computed(() => {
    const seconds = this.secondsLeft();
    return `${seconds} ${seconds === 1 ? 'segundo' : 'segundos'}`;
  });
}

/**
 * Abre o aviso como diálogo modal que não fecha ao clicar fora nem com Esc: a pessoa precisa
 * escolher. Só "Continuar conectado" chama `onKeep`; fechar por `close()` não conta como atividade.
 */
export function openIdleWarning(
  injector: Injector,
  secondsLeft: Signal<number>,
  onKeep: () => void,
): { close(): void } {
  const ref = injector
    .get(MatDialog)
    .open<IdleWarningDialog, IdleWarningData, boolean>(IdleWarningDialog, {
      data: { secondsLeft },
      disableClose: true,
      role: 'alertdialog',
      width: '420px',
      maxWidth: 'calc(100vw - 32px)',
    });
  ref.afterClosed().subscribe((keep) => {
    if (keep) {
      onKeep();
    }
  });
  return { close: (): void => ref.close() };
}
