import {
  Announcement,
  AnnouncementCategory,
  AnnouncementScope,
} from '../../core/announcements/announcements.models';
import { Unit } from '../../core/units/units.models';

export const CATEGORY_OPTIONS: readonly { value: AnnouncementCategory; label: string }[] = [
  { value: 'general', label: 'Geral' },
  { value: 'maintenance', label: 'Manutenção' },
  { value: 'important', label: 'Importante' },
];

const MOMENT = new Intl.DateTimeFormat('pt-BR', {
  timeZone: 'America/Sao_Paulo',
  day: '2-digit',
  month: '2-digit',
  year: 'numeric',
  hour: '2-digit',
  minute: '2-digit',
  hourCycle: 'h23',
});

export function categoryLabel(category: AnnouncementCategory): string {
  return CATEGORY_OPTIONS.find((option) => option.value === category)?.label ?? 'Geral';
}

/** Para quem vai o comunicado, em palavras. A unidade vem da lista das ativas; uma que saiu dela fica sem número. */
export function scopeLabel(item: Announcement, units: readonly Unit[]): string {
  switch (item.scope) {
    case 'block':
      return `Bloco ${item.block ?? ''}`.trim();
    case 'unit': {
      const target = units.find((candidate) => candidate.id === item.unitId);
      if (target === undefined) {
        return 'Uma unidade que não está mais ativa';
      }
      return target.block
        ? `Unidade ${target.number} do bloco ${target.block}`
        : `Unidade ${target.number}`;
    }
    default:
      return 'Condomínio inteiro';
  }
}

/**
 * Cor de cada grau: azul para o aviso comum, âmbar para a manutenção (algo em andamento) e vermelho para o importante.
 * Os nomes das classes ficam inteiros aqui para o Tailwind achá-los; o texto da etiqueta continua dizendo o grau.
 */
const CATEGORY_CLASSES: Record<AnnouncementCategory, string> = {
  general: 'bg-tag-blue-container text-tag-blue',
  maintenance: 'bg-tag-amber-container text-tag-amber',
  important: 'bg-tag-red-container text-tag-red',
};

/** Cor de cada público, em cores que não se repetem com as do grau. */
const SCOPE_CLASSES: Record<AnnouncementScope, string> = {
  condominium: 'bg-tag-teal-container text-tag-teal',
  block: 'bg-tag-purple-container text-tag-purple',
  unit: 'bg-tag-pink-container text-tag-pink',
};

export function categoryClasses(category: AnnouncementCategory): string {
  return CATEGORY_CLASSES[category] ?? CATEGORY_CLASSES.general;
}

export function scopeClasses(scope: AnnouncementScope): string {
  return SCOPE_CLASSES[scope] ?? SCOPE_CLASSES.condominium;
}

/** Quantos leram chama a atenção do síndico em verde; sem ninguém para ler, não há o que destacar. */
export function readersClasses(item: Pick<Announcement, 'recipients'>): string {
  return item.recipients === 0
    ? 'text-on-surface-variant'
    : 'bg-tag-green-container text-tag-green';
}

/** "3 de 10 leram". Sem ninguém morando no alcance, não há quem leia. */
export function readersLabel(item: Pick<Announcement, 'readers' | 'recipients'>): string {
  if (item.recipients === 0) {
    return 'Ninguém mora aqui ainda para ler';
  }
  return `${item.readers} de ${item.recipients} ${item.recipients === 1 ? 'leu' : 'leram'}`;
}

/** "22/11/2026 às 10:50", sempre na hora de Brasília, para o síndico ler igual em qualquer computador. */
export function formatMoment(iso: string): string {
  const moment = new Date(iso);
  if (Number.isNaN(moment.getTime())) {
    return iso;
  }
  const [date, time] = MOMENT.format(moment).split(', ');
  return `${date} às ${time}`;
}
