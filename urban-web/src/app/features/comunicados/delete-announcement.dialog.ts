import { ChangeDetectionStrategy, Component, Injector } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Announcement } from '../../core/announcements/announcements.models';

export interface DeleteAnnouncementDialogConfig {
  readonly announcement: Announcement;
  /** Exclui o comunicado. Devolve nulo quando deu certo ou o texto do erro (o diálogo continua aberto). */
  readonly save: () => Promise<string | null>;
}

/** Confirma a exclusão de um comunicado. */
@Component({
  selector: 'urban-delete-announcement-dialog',
  template: '',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DeleteAnnouncementDialog {}

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
