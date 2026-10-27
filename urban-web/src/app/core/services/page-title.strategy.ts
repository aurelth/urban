import { Injectable, Provider, inject } from '@angular/core';
import { Title } from '@angular/platform-browser';
import { RouterStateSnapshot, TitleStrategy } from '@angular/router';

export const APP_TITLE = 'Urban - Gestão condominial';

/** O nome da aplicação vem primeiro: as abas são estreitas e o que fica no fim é cortado. */
export function composeTitle(pageTitle: string | undefined): string {
  return pageTitle ? `${APP_TITLE} | ${pageTitle}` : APP_TITLE;
}

/**
 * Sem uma estratégia própria, o Angular troca o título do index.html pelo que a rota declara
 * (ex.: "Entrar"). Aqui o nome da aplicação é acrescentado em um só lugar, e as rotas informam
 * apenas o nome da tela.
 */
@Injectable({ providedIn: 'root' })
export class UrbanTitleStrategy extends TitleStrategy {
  private readonly title = inject(Title);

  override updateTitle(snapshot: RouterStateSnapshot): void {
    this.title.setTitle(composeTitle(this.buildTitle(snapshot)));
  }
}

export function providePageTitle(): Provider {
  return { provide: TitleStrategy, useExisting: UrbanTitleStrategy };
}
