import {
  AnnouncementCategory,
  MyAnnouncement,
} from '../../core/announcements/announcements.models';

/** Para quem o aviso vale, do ponto de vista de quem lê: "Sua unidade 101 do bloco A". */
export function muralScopeLabel(item: MyAnnouncement): string {
  throw new Error(`Não implementado: ${item.id}`);
}

/** Quando o aviso saiu, em palavras curtas; passada uma semana, a data. O instante exato fica no `title` do elemento. */
export function relativeTime(iso: string, now: Date): string {
  throw new Error(`Não implementado: ${iso} ${now.toISOString()}`);
}

/** A frase do topo do mural. */
export function unreadSummary(unread: number): string {
  throw new Error(`Não implementado: ${String(unread)}`);
}

/** O rótulo do botão que marca tudo como lido: sem categoria, o mural inteiro; com ela, só os avisos dela. */
export function markAllLabel(category: AnnouncementCategory | null): string {
  throw new Error(`Não implementado: ${String(category)}`);
}

/** O que a tela diz depois de marcar tudo como lido. */
export function markedNotice(marked: number): string {
  throw new Error(`Não implementado: ${String(marked)}`);
}

/** O ícone de cada categoria; todos já estão no glob de assets do angular.json. */
export function categoryIcon(category: AnnouncementCategory): string {
  throw new Error(`Não implementado: ${category}`);
}

/** A cor da barra lateral do cartão, que acompanha a categoria. */
export function accentClasses(category: AnnouncementCategory): string {
  throw new Error(`Não implementado: ${category}`);
}
