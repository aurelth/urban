import { signal } from '@angular/core';
import { jest } from '@jest/globals';
import { Subject } from 'rxjs';
import { MuralLive, MuralUpdate } from '../core/announcements/mural-live';

/** O mural ao vivo de mentira: o teste decide quando o servidor avisa e vê a contagem que o mural passou ao menu. */
export function fakeLive(): {
  provider: { provide: typeof MuralLive; useValue: unknown };
  push: (update: MuralUpdate) => void;
  setUnread: jest.Mock<(unread: number) => void>;
} {
  const updates = new Subject<MuralUpdate>();
  const setUnread = jest.fn<(unread: number) => void>();
  return {
    provider: {
      provide: MuralLive,
      useValue: { unread: signal(0), updates: updates.asObservable(), setUnread },
    },
    push: (update) => updates.next(update),
    setUnread,
  };
}
