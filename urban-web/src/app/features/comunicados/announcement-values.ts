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

/** Cor do grau (etiqueta colorida): ainda por fazer. */
export function categoryClasses(_category: AnnouncementCategory): string {
  return '';
}

/** Cor do público: ainda por fazer. */
export function scopeClasses(_scope: AnnouncementScope): string {
  return '';
}

/** Cor de quantos leram: ainda por fazer. */
export function readersClasses(_item: Pick<Announcement, 'recipients'>): string {
  return '';
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
