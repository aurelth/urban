import { DOCUMENT, inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';

const STORAGE_KEY = 'urban.sidebar';
const COLLAPSED = 'collapsed';
const PINNED = 'pinned';

function readCollapsed(doc: Document): boolean {
  try {
    return doc.defaultView?.localStorage.getItem(STORAGE_KEY) === COLLAPSED;
  } catch {
    // localStorage indisponível (modo privado, bloqueio): o menu começa fixado.
    return false;
  }
}

function saveCollapsed(doc: Document, collapsed: boolean): void {
  try {
    doc.defaultView?.localStorage.setItem(STORAGE_KEY, collapsed ? COLLAPSED : PINNED);
  } catch {
    // Sem persistência: a escolha vale apenas para esta visita.
  }
}

/**
 * Se o menu lateral do computador está recolhido (só ícones) ou fixado (com os nomes). A escolha é da pessoa e
 * fica guardada no navegador; no celular ela não vale, porque lá o menu é uma gaveta que sempre mostra os nomes.
 */
export const SidebarStore = signalStore(
  { providedIn: 'root' },
  withState<{ collapsed: boolean }>({ collapsed: false }),
  withMethods((store) => {
    const doc = inject(DOCUMENT);
    const set = (collapsed: boolean): void => {
      patchState(store, { collapsed });
      saveCollapsed(doc, collapsed);
    };
    return {
      set,
      toggle: (): void => set(!store.collapsed()),
    };
  }),
  withHooks({
    onInit(store) {
      patchState(store, { collapsed: readCollapsed(inject(DOCUMENT)) });
    },
  }),
);
