import { EnvironmentProviders, inject, provideAppInitializer } from '@angular/core';
import { ThemeStore } from './theme.store';

/**
 * O ThemeStore aplica a classe do tema ao ser criado. Se só o layout logado o pedisse, as telas
 * públicas (como a de entrada) nunca receberiam o tema escuro. Criá-lo na abertura vale para todas.
 */
export function provideTheme(): EnvironmentProviders {
  return provideAppInitializer(() => {
    inject(ThemeStore);
  });
}
