import {
  AnnouncementCategory,
  MyAnnouncement,
} from '../../core/announcements/announcements.models';

const MINUTE_MS = 60_000;
const HOUR_MS = 60 * MINUTE_MS;
const DAY_MS = 24 * HOUR_MS;
const DAYS_BEFORE_DATE = 7;

const BRASILIA_DAY = new Intl.DateTimeFormat('en-CA', {
  timeZone: 'America/Sao_Paulo',
  year: 'numeric',
  month: '2-digit',
  day: '2-digit',
});

const BRASILIA_DATE = new Intl.DateTimeFormat('pt-BR', {
  timeZone: 'America/Sao_Paulo',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
});

/** Para quem o aviso vale, do ponto de vista de quem lê: "Sua unidade 101 do bloco A". */
export function muralScopeLabel(item: MyAnnouncement): string {
  switch (item.scope) {
    case 'block':
      return `Bloco ${item.block ?? ''}`.trim();
    case 'unit': {
      if (item.unit === null) {
        return 'Sua unidade';
      }
      return item.unit.block
        ? `Sua unidade ${item.unit.number} do bloco ${item.unit.block}`
        : `Sua unidade ${item.unit.number}`;
    }
    default:
      return 'Condomínio inteiro';
  }
}

/** O número do dia no calendário de Brasília, para contar "ontem" e "há 3 dias" como a pessoa conta. */
function brasiliaDay(moment: Date): number {
  const [year, month, day] = BRASILIA_DAY.format(moment).split('-').map(Number);
  return Date.UTC(year, month - 1, day) / DAY_MS;
}

/** Quando o aviso saiu, em palavras curtas; passada uma semana, a data. O instante exato fica no `title` do elemento. */
export function relativeTime(iso: string, now: Date): string {
  const moment = new Date(iso);
  if (Number.isNaN(moment.getTime())) {
    return iso;
  }
  const elapsed = now.getTime() - moment.getTime();
  if (elapsed < MINUTE_MS) {
    return 'agora há pouco';
  }
  if (elapsed < HOUR_MS) {
    return `há ${String(Math.floor(elapsed / MINUTE_MS))} min`;
  }
  if (elapsed < DAY_MS) {
    return `há ${String(Math.floor(elapsed / HOUR_MS))} h`;
  }
  const days = brasiliaDay(now) - brasiliaDay(moment);
  if (days <= 1) {
    return 'ontem';
  }
  return days < DAYS_BEFORE_DATE ? `há ${String(days)} dias` : BRASILIA_DATE.format(moment);
}

/** A frase do topo do mural. */
export function unreadSummary(unread: number): string {
  if (unread === 0) {
    return 'Você está em dia';
  }
  return `Você tem ${String(unread)} ${unread === 1 ? 'aviso novo' : 'avisos novos'}`;
}

const MARK_ALL_LABELS: Record<AnnouncementCategory, string> = {
  general: 'Marcar avisos gerais como lidos',
  maintenance: 'Marcar avisos de manutenção como lidos',
  important: 'Marcar avisos importantes como lidos',
};

/** O rótulo do botão que marca tudo como lido: sem categoria, o mural inteiro; com ela, só os avisos dela. */
export function markAllLabel(category: AnnouncementCategory | null): string {
  return category === null ? 'Marcar todos como lidos' : MARK_ALL_LABELS[category];
}

/** O que a tela diz depois de marcar tudo como lido. */
export function markedNotice(marked: number): string {
  if (marked === 0) {
    return 'Nenhum aviso novo para marcar';
  }
  return marked === 1
    ? '1 aviso marcado como lido'
    : `${String(marked)} avisos marcados como lidos`;
}

const CATEGORY_ICONS: Record<AnnouncementCategory, string> = {
  general: 'info',
  maintenance: 'build',
  important: 'warning',
};

/** O ícone de cada categoria; todos já estão no glob de assets do angular.json. */
export function categoryIcon(category: AnnouncementCategory): string {
  return CATEGORY_ICONS[category] ?? CATEGORY_ICONS.general;
}

/** A cor da barra lateral do cartão, que acompanha a categoria (nomes inteiros para o Tailwind achá-los). */
const ACCENT_CLASSES: Record<AnnouncementCategory, string> = {
  general: 'bg-tag-blue',
  maintenance: 'bg-tag-amber',
  important: 'bg-tag-red',
};

export function accentClasses(category: AnnouncementCategory): string {
  return ACCENT_CLASSES[category] ?? ACCENT_CLASSES.general;
}
