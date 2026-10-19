import { DOCUMENT, computed, effect, inject } from '@angular/core';
import {
  patchState,
  signalStore,
  withComputed,
  withHooks,
  withMethods,
  withState,
} from '@ngrx/signals';

export type ThemeMode = 'light' | 'dark';

const STORAGE_KEY = 'urban.theme';
const DARK_CLASS = 'app-dark';

function readInitialMode(doc: Document): ThemeMode {
  try {
    const saved = doc.defaultView?.localStorage.getItem(STORAGE_KEY);
    if (saved === 'light' || saved === 'dark') {
      return saved;
    }
  } catch {
    // localStorage indisponivel (modo privado, bloqueio): segue para a preferencia do sistema.
  }
  return doc.defaultView?.matchMedia?.('(prefers-color-scheme: dark)').matches ? 'dark' : 'light';
}

function saveMode(doc: Document, mode: ThemeMode): void {
  try {
    doc.defaultView?.localStorage.setItem(STORAGE_KEY, mode);
  } catch {
    // Sem persistencia: o tema vale apenas para esta sessao.
  }
}

export const ThemeStore = signalStore(
  { providedIn: 'root' },
  withState<{ mode: ThemeMode }>({ mode: 'light' }),
  withComputed(({ mode }) => ({
    isDark: computed(() => mode() === 'dark'),
  })),
  withMethods((store) => {
    const doc = inject(DOCUMENT);
    const set = (mode: ThemeMode): void => {
      patchState(store, { mode });
      saveMode(doc, mode);
    };
    return {
      set,
      toggle: (): void => set(store.mode() === 'dark' ? 'light' : 'dark'),
    };
  }),
  withHooks({
    onInit(store) {
      const doc = inject(DOCUMENT);
      patchState(store, { mode: readInitialMode(doc) });
      effect(() => {
        doc.documentElement.classList.toggle(DARK_CLASS, store.mode() === 'dark');
      });
    },
  }),
);
