import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MyAnnouncement } from '../../core/announcements/announcements.models';

/** Um aviso do mural: o que diz, para quem vale, quando saiu e se o morador já leu. */
@Component({
  selector: 'urban-mural-card',
  template: '',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MuralCard {
  readonly item = input.required<MyAnnouncement>();
  /** A leitura deste aviso está indo para a API. */
  readonly marking = input(false);
  readonly markRead = output();
}
