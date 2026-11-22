import { ChangeDetectionStrategy, Component, Injector } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';
import { Announcement, AnnouncementPayload } from '../../core/announcements/announcements.models';
import { Unit } from '../../core/units/units.models';

/** O que o diálogo mostra e o que ele faz com o comunicado digitado. */
export interface AnnouncementDialogConfig {
  /** O comunicado que está sendo editado; nulo quando é um novo. */
  readonly announcement: Announcement | null;
  /** As unidades ativas, de onde saem os blocos e as unidades que o síndico pode escolher. */
  readonly units: readonly Unit[];
  /**
   * Grava o comunicado. Devolve nulo quando deu certo (o diálogo fecha) ou o texto do erro (o diálogo continua aberto,
   * com o que foi digitado, para a pessoa corrigir).
   */
  readonly save: (payload: AnnouncementPayload) => Promise<string | null>;
}

/** Criar e editar um comunicado. */
@Component({
  selector: 'urban-announcement-dialog',
  template: '',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnnouncementDialog {}

/**
 * Abre o diálogo como modal que não fecha ao clicar fora nem com Esc, para ninguém perder o que digitou sem querer.
 * Responde verdadeiro quando o comunicado foi gravado e falso quando a pessoa cancelou.
 */
export function openAnnouncementDialog(
  injector: Injector,
  config: AnnouncementDialogConfig,
): Promise<boolean> {
  const ref = injector
    .get(MatDialog)
    .open<AnnouncementDialog, AnnouncementDialogConfig, boolean>(AnnouncementDialog, {
      data: config,
      disableClose: true,
      role: 'dialog',
      width: '640px',
      maxWidth: 'calc(100vw - 32px)',
    });
  return new Promise<boolean>((resolve) => {
    ref.afterClosed().subscribe((saved) => resolve(saved === true));
  });
}
