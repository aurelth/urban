import { EnvironmentProviders, inject, provideAppInitializer } from '@angular/core';
import { DomSanitizer } from '@angular/platform-browser';
import { MatIconRegistry } from '@angular/material/icon';

/**
 * Icones do Material Symbols servidos pelo proprio app (sem CDN do Google).
 * Cada icone usado precisa ser listado no "glob" de assets do angular.json, que copia o SVG
 * de node_modules/@material-symbols/svg-400/outlined para /icons. Uso: <mat-icon svgIcon="dark_mode" />.
 */
export function provideUrbanIcons(): EnvironmentProviders {
  return provideAppInitializer(() => {
    const sanitizer = inject(DomSanitizer);
    inject(MatIconRegistry).addSvgIconResolver((nome) =>
      sanitizer.bypassSecurityTrustResourceUrl(`icons/${nome}.svg`),
    );
  });
}
