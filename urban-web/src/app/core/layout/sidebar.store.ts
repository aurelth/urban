import { DOCUMENT, inject } from '@angular/core';
import { patchState, signalStore, withHooks, withMethods, withState } from '@ngrx/signals';

const STORAGE_KEY = 'urban.sidebar';
const COLLAPSED = 'collapsed';
const PINNED = 'pinned';
const GROUPS_KEY = 'urban.sidebar-groups';

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

function readCollapsedGroups(doc: Document): readonly string[] {
  try {
    const saved: unknown = JSON.parse(doc.defaultView?.localStorage.getItem(GROUPS_KEY) ?? '[]');
    // O que vem do armazenamento é entrada não confiável: só uma lista de textos vale.
    return Array.isArray(saved)
      ? [...new Set(saved.filter((id): id is string => typeof id === 'string'))]
      : [];
  } catch {
    return [];
  }
}

function saveCollapsedGroups(doc: Document, ids: readonly string[]): void {
  try {
    doc.defaultView?.localStorage.setItem(GROUPS_KEY, JSON.stringify(ids));
  } catch {
    // Sem persistência: a escolha vale apenas para esta visita.
  }
}

/**
 * Se o menu lateral do computador está recolhido (só ícones) ou fixado (com os nomes). A escolha é da pessoa e
 * fica guardada no navegador; no celular ela não vale, porque lá o menu é uma gaveta que sempre mostra os nomes.
 * Também guarda quais seções do menu (Condomínio, Financeiro...) a pessoa recolheu: as outras ficam abertas.
 */
export const SidebarStore = signalStore(
  { providedIn: 'root' },
  withState<{ collapsed: boolean; collapsedGroups: readonly string[] }>({
    collapsed: false,
    collapsedGroups: [],
  }),
  withMethods((store) => {
    const doc = inject(DOCUMENT);
    const set = (collapsed: boolean): void => {
      patchState(store, { collapsed });
      saveCollapsed(doc, collapsed);
    };
    return {
      set,
      toggle: (): void => set(!store.collapsed()),
      isGroupCollapsed: (id: string): boolean => store.collapsedGroups().includes(id),
      toggleGroup: (id: string): void => {
        const current = store.collapsedGroups();
        const collapsedGroups = current.includes(id)
          ? current.filter((other) => other !== id)
          : [...current, id];
        patchState(store, { collapsedGroups });
        saveCollapsedGroups(doc, collapsedGroups);
      },
    };
  }),
  withHooks({
    onInit(store) {
      const doc = inject(DOCUMENT);
      patchState(store, {
        collapsed: readCollapsed(doc),
        collapsedGroups: readCollapsedGroups(doc),
      });
    },
  }),
);
