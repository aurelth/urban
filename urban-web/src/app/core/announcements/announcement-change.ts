/** Como o servidor chama o aviso do mural no SignalR (`AnnouncementsHub.ChangedMethod`). */
export const ANNOUNCEMENT_CHANGED = 'announcementChanged';

/** Onde o navegador abre a conexão do mural (o nginx encaminha `/hubs/` à API). */
export const ANNOUNCEMENTS_HUB_PATH = '/hubs/announcements';

export type AnnouncementChangeKind = 'published' | 'updated' | 'removed';

/** O que o servidor avisa: só o que mudou e qual comunicado; o conteúdo a tela busca pela API, que confere o alcance. */
export interface AnnouncementChange {
  readonly kind: AnnouncementChangeKind;
  readonly id: string;
}

/** Lê o que veio do hub. Tudo o que chega de fora é entrada não confiável: formato diferente é ignorado. */
export function parseChange(message: unknown): AnnouncementChange | null {
  if (typeof message !== 'object' || message === null || Array.isArray(message)) {
    return null;
  }
  const { kind, id } = message as Record<string, unknown>;
  if (!isKind(kind) || typeof id !== 'string' || id === '') {
    return null;
  }
  return { kind, id };
}

function isKind(value: unknown): value is AnnouncementChangeKind {
  return value === 'published' || value === 'updated' || value === 'removed';
}
