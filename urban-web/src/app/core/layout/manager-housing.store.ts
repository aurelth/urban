import { computed } from '@angular/core';
import { signalStore, withComputed, withMethods, withState } from '@ngrx/signals';
import { MyMonthAccess } from '../finance/my-month.models';

interface HousingState {
  access: MyMonthAccess | null;
}

/** Se o síndico mora no condomínio (e então tem fatura própria para ver). */
export const ManagerHousingStore = signalStore(
  { providedIn: 'root' },
  withState<HousingState>({ access: null }),
  withComputed(() => ({ livesHere: computed(() => false) })),
  withMethods(() => ({
    load: (_userId: string): Promise<void> => Promise.resolve(),
    reset: (): void => undefined,
  })),
);
