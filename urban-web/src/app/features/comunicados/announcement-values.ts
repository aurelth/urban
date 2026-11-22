import { Announcement, AnnouncementCategory } from '../../core/announcements/announcements.models';
import { Unit } from '../../core/units/units.models';

export const CATEGORY_OPTIONS: readonly { value: AnnouncementCategory; label: string }[] = [];

export function categoryLabel(_category: AnnouncementCategory): string {
  return '';
}

export function scopeLabel(_item: Announcement, _units: readonly Unit[]): string {
  return '';
}

export function readersLabel(_item: Pick<Announcement, 'readers' | 'recipients'>): string {
  return '';
}

export function formatMoment(_iso: string): string {
  return '';
}
