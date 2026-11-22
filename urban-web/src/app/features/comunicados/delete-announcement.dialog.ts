import { ChangeDetectionStrategy, Component, Injector, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import {
  MAT_DIALOG_DATA,
  MatDialog,
  MatDialogActions,
  MatDialogContent,
  MatDialogRef,
  MatDialogTitle,
} from '@angular/material/dialog';
import { MatIcon } from '@angular/material/icon';
import { Announcement } from '../../core/announcements/announcements.models';

export interface DeleteAnnouncementDialogConfig {
  readonly announcement: Announcement;
  /** Exclui o comunicado. Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: () => Promise<string | null>;
}

/** Confirma a exclusão de um comunicado, que some do mural dos moradores junto com os registros de leitura. */
@Component({
  selector: 'urban-delete-announcement-dialog',
  imports: [MatButton, MatDialogActions, MatDialogContent, MatDialogTitle, MatIcon],
  templateUrl: './delete-announcement.dialog.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeleteAnnouncementDialog {
  protected readonly config = inject<DeleteAnnouncementDialogConfig>(MAT_DIALOG_DATA);
  protected readonly ref = inject<MatDialogRef<DeleteAnnouncementDialog, boolean>>(MatDialogRef);

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

/** Abre a confirmação como modal que não fecha ao clicar fora nem com Esc. Responde verdadeiro quando excluiu. */
export function openDeleteAnnouncementDialog(
  injector: Injector,
  config: DeleteAnnouncementDialogConfig,
): Promise<boolean> {
  const ref = injector
    .get(MatDialog)
    .open<DeleteAnnouncementDialog, DeleteAnnouncementDialogConfig, boolean>(
      DeleteAnnouncementDialog,
      {
        data: config,
        disableClose: true,
        role: 'dialog',
        width: '520px',
        maxWidth: 'calc(100vw - 32px)',
      },
    );
  return new Promise<boolean>((resolve) => {
    ref.afterClosed().subscribe((deleted) => resolve(deleted === true));
  });
}
