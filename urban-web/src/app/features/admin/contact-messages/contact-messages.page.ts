import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { MatAnchor, MatButton } from '@angular/material/button';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { ContactMessage, ContactMessagesFilter } from '../../../core/admin/contact-messages.models';
import { describeProblem } from '../../../core/http/problem-message';
import { MenuOption, MenuSelect } from '../../../shared/ui/menu-select/menu-select';
import { formatRequestedAt } from '../registration-requests/requested-at';
import { MARK_READ_MESSAGES } from './contact-messages.messages';
import { ContactMessagesStore } from './contact-messages.store';

const STATUS_OPTIONS: readonly MenuOption[] = [
  { value: '', label: 'Todas' },
  { value: 'unread', label: 'Não lidas' },
];

/** Link que abre o e-mail do administrador já endereçado ao síndico, com o assunto da mensagem. */
export function replyLink(message: ContactMessage): string {
  return `mailto:${message.senderEmail}?subject=${encodeURIComponent(`Re: ${message.subject}`)}`;
}

/**
 * Caixa de mensagens que os síndicos mandam ao dono da plataforma. Não há conversa dentro do sistema: a resposta vai
 * por e-mail, e aqui só se lê e se marca como lida.
 */
@Component({
  selector: 'urban-contact-messages-page',
  imports: [MatButton, MatAnchor, MatFormField, MatLabel, MatIcon, MenuSelect],
  templateUrl: './contact-messages.page.html',
  providers: [ContactMessagesStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ContactMessagesPage implements OnInit {
  protected readonly store = inject(ContactMessagesStore);

  protected readonly notice = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);

  protected readonly statusOptions = STATUS_OPTIONS;
  protected readonly formatDate = formatRequestedAt;
  protected readonly replyLink = replyLink;

  ngOnInit(): void {
    void this.load();
  }

  protected load(): Promise<void> {
    return this.store.load();
  }

  protected onStatus(value: string): void {
    const status: ContactMessagesFilter = value === 'unread' ? 'unread' : '';
    void this.store.filter({ status });
  }

  protected clearFilters(): void {
    void this.store.filter({ status: '' });
  }

  protected goTo(page: number): void {
    void this.store.goToPage(page);
  }

  protected async markRead(message: ContactMessage): Promise<void> {
    this.notice.set(null);
    this.actionError.set(null);
    const problem = await this.store.markRead(message);
    if (problem === null) {
      this.notice.set('Mensagem marcada como lida');
    } else {
      this.actionError.set(describeProblem(problem, MARK_READ_MESSAGES));
    }
  }
}
