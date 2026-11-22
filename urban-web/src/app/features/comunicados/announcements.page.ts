import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { Announcement, AnnouncementPayload } from '../../core/announcements/announcements.models';
import { ApiProblem } from '../../core/http/api-problem';
import { describeProblem } from '../../core/http/problem-message';
import { ExpandableText } from '../../shared/ui/expandable-text/expandable-text';
import { openAnnouncementDialog } from './announcement.dialog';
import {
  categoryClasses,
  categoryLabel,
  formatMoment,
  readersClasses,
  readersLabel,
  scopeClasses,
  scopeLabel,
} from './announcement-values';
import { ANNOUNCEMENT_DELETE_MESSAGES, ANNOUNCEMENT_FORM_MESSAGES } from './announcements.messages';
import { AnnouncementsStore } from './announcements.store';
import { openDeleteAnnouncementDialog } from './delete-announcement.dialog';

/**
 * Comunicados do síndico: a lista dos avisos do condomínio, do mais novo para o mais antigo, com quantos moradores já
 * leram cada um, e as ações de publicar, editar e excluir.
 */
@Component({
  selector: 'urban-announcements-page',
  imports: [ExpandableText, MatButton, MatIconButton, MatIcon],
  templateUrl: './announcements.page.html',
  providers: [AnnouncementsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class AnnouncementsPage implements OnInit {
  protected readonly store = inject(AnnouncementsStore);
  private readonly injector = inject(Injector);

  protected readonly notice = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);

  protected readonly category = categoryLabel;
  protected readonly categoryStyle = categoryClasses;
  protected readonly scopeStyle = scopeClasses;
  protected readonly readersStyle = readersClasses;
  protected readonly moment = formatMoment;
  protected readonly readers = readersLabel;
  protected readonly scope = (item: Announcement): string => scopeLabel(item, this.store.units());

  ngOnInit(): void {
    void this.load();
  }

  protected load(): Promise<void> {
    return this.store.load();
  }

  protected goTo(page: number): void {
    void this.store.goToPage(page);
  }

  protected async create(): Promise<void> {
    this.clearMessages();
    await openAnnouncementDialog(this.injector, {
      announcement: null,
      units: this.store.units(),
      save: async (payload) => {
        const message = await this.saveAs(this.store.create(payload));
        if (message === null) {
          this.notice.set(`Comunicado "${payload.title}" publicado`);
        }
        return message;
      },
    });
  }

  protected async edit(target: Announcement): Promise<void> {
    this.clearMessages();
    await openAnnouncementDialog(this.injector, {
      announcement: target,
      units: this.store.units(),
      save: async (payload: AnnouncementPayload) => {
        const message = await this.saveAs(this.store.update(target, payload));
        if (message === null) {
          this.notice.set(`Comunicado "${payload.title}" atualizado`);
        }
        return message;
      },
    });
  }

  protected async remove(target: Announcement): Promise<void> {
    this.clearMessages();
    await openDeleteAnnouncementDialog(this.injector, {
      announcement: target,
      save: async () => {
        const problem = await this.store.remove(target);
        if (problem === null) {
          this.notice.set(`Comunicado "${target.title}" excluído`);
          return null;
        }
        return describeProblem(problem, ANNOUNCEMENT_DELETE_MESSAGES);
      },
    });
  }

  /** Texto do erro para o diálogo, ou nulo quando a gravação deu certo. */
  private async saveAs(request: Promise<ApiProblem | null>): Promise<string | null> {
    const problem = await request;
    return problem === null ? null : describeProblem(problem, ANNOUNCEMENT_FORM_MESSAGES);
  }

  private clearMessages(): void {
    this.notice.set(null);
    this.actionError.set(null);
  }
}
