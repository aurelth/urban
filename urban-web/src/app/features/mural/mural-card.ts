import { ChangeDetectionStrategy, Component, computed, inject, input, output } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { MyAnnouncement } from '../../core/announcements/announcements.models';
import { NOW } from '../../core/finance/clock';
import { ExpandableText } from '../../shared/ui/expandable-text/expandable-text';
import {
  categoryClasses,
  categoryLabel,
  formatMoment,
  scopeClasses,
} from '../comunicados/announcement-values';
import { accentClasses, categoryIcon, muralScopeLabel, relativeTime } from './mural-values';

/** O aviso ainda não lido se destaca do resto pelo fundo e pela borda, além do "Novo" em texto. */
const UNREAD_CLASSES = 'border-primary/40 bg-primary/10';
const READ_CLASSES = 'border-outline-variant bg-surface-container';

/** Um aviso do mural: o que diz, para quem vale, quando saiu e se o morador já leu. */
@Component({
  selector: 'urban-mural-card',
  imports: [MatButton, MatIcon, ExpandableText],
  templateUrl: './mural-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class MuralCard {
  readonly item = input.required<MyAnnouncement>();
  /** A leitura deste aviso está indo para a API. */
  readonly marking = input(false);
  readonly markRead = output();

  private readonly now = inject(NOW);

  protected readonly unread = computed(() => !this.item().read);
  protected readonly frameClasses = computed(() => (this.unread() ? UNREAD_CLASSES : READ_CLASSES));
  protected readonly accent = computed(() => accentClasses(this.item().category));
  protected readonly categoryText = computed(() => categoryLabel(this.item().category));
  protected readonly categoryTone = computed(() => categoryClasses(this.item().category));
  protected readonly icon = computed(() => categoryIcon(this.item().category));
  protected readonly scopeText = computed(() => muralScopeLabel(this.item()));
  protected readonly scopeTone = computed(() => scopeClasses(this.item().scope));
  protected readonly published = computed(() => relativeTime(this.item().createdAt, this.now()));
  protected readonly publishedAt = computed(() => formatMoment(this.item().createdAt));

  protected readonly formatMoment = formatMoment;
}
