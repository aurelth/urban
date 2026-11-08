import { ComponentType } from '@angular/cdk/portal';
import { Injector } from '@angular/core';
import { MatDialog } from '@angular/material/dialog';

/**
 * Abre o diálogo como modal que não fecha ao clicar fora nem com Esc, para ninguém perder o que digitou sem querer.
 * Responde verdadeiro quando algo foi gravado e falso quando a pessoa cancelou.
 */
export function openFinanceDialog<TComponent, TData>(
  injector: Injector,
  component: ComponentType<TComponent>,
  data: TData,
): Promise<boolean> {
  const ref = injector.get(MatDialog).open<TComponent, TData, boolean>(component, {
    data,
    disableClose: true,
    role: 'dialog',
    width: '520px',
    maxWidth: 'calc(100vw - 32px)',
  });
  return new Promise<boolean>((resolve) => {
    ref.afterClosed().subscribe((saved) => resolve(saved === true));
  });
}
