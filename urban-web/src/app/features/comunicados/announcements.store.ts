import { computed } from '@angular/core';
import { signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { Announcement, AnnouncementPayload } from '../../core/announcements/announcements.models';
import { ApiProblem } from '../../core/http/api-problem';
import { Unit } from '../../core/units/units.models';

export type AnnouncementsStatus = 'loading' | 'ready' | 'error';

export const CONCURRENCY_CONFLICT = 'concurrency.conflict';

interface AnnouncementsState {
  status: AnnouncementsStatus;
  page: number;
  items: readonly Announcement[];
  total: number;
  units: readonly Unit[];
  busy: boolean;
}

/** Estado da lista de comunicados do síndico. Fornecido no componente, então cada visita começa do zero. */
export const AnnouncementsStore = signalStore(
  withState<AnnouncementsState>({
    status: 'loading',
    page: 1,
    items: [],
    total: 0,
    units: [],
    busy: false,
  }),
  withComputed(() => ({
    pageCount: computed(() => 1),
    blocks: computed<readonly string[]>(() => []),
  })),
  withMethods(() => ({
    load: async (): Promise<void> => undefined,
    goToPage: async (_page: number): Promise<void> => undefined,
    create: async (_payload: AnnouncementPayload): Promise<ApiProblem | null> => null,
    update: async (
      _target: Announcement,
      _payload: AnnouncementPayload,
    ): Promise<ApiProblem | null> => null,
    remove: async (_target: Announcement): Promise<ApiProblem | null> => null,
  })),
);
