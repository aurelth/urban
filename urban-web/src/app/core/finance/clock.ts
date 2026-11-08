import { InjectionToken } from '@angular/core';

/** O relógio das telas do financeiro; os testes trocam por uma data fixa. */
export const NOW = new InjectionToken<() => Date>('NOW', {
  providedIn: 'root',
  factory: () => () => new Date(),
});
