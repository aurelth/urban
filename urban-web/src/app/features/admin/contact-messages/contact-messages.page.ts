import { ChangeDetectionStrategy, Component, OnInit, inject, signal } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { ContactMessage, ContactMessagesFilter } from '../../../core/admin/contact-messages.models';
import { describeProblem } from '../../../core/http/problem-message';
import { MenuOption, MenuSelect } from '../../../shared/ui/menu-select/menu-select';
import { formatRequestedAt } from '../registration-requests/requested-at';
import { MARK_READ_MESSAGES } from './contact-messages.messages';
import { ContactMessagesStore } from './contact-messages.store';
import { ContactReplyForm } from './contact-reply-form';
import { formatFileSize } from './reply-attachments';

const STATUS_OPTIONS: readonly MenuOption[] = [
  { value: '', label: 'Todas' },
  { value: 'unread', label: 'Não lidas' },
];

/**
 * Caixa de mensagens que os síndicos mandam ao dono da plataforma. O administrador lê, marca como lida e responde aqui
 * mesmo: a resposta (com anexos) vai por e-mail ao síndico e fica registrada no cartão. A conversa não continua dentro do
 * sistema: o síndico responde pelo e-mail do administrador.
 */
@Component({
  selector: 'urban-contact-messages-page',
  imports: [MatButton, MatFormField, MatLabel, MatIcon, MenuSelect, ContactReplyForm],
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
  protected readonly formatSize = formatFileSize;

  /** Mensagens com o formulário de resposta aberto; cada uma guarda o seu rascunho enquanto estiver aberta. */
  protected readonly replying = signal<ReadonlySet<string>>(new Set());

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

  protected openReply(message: ContactMessage): void {
    this.replying.update((open) => new Set(open).add(message.id));
  }

  protected closeReply(message: ContactMessage): void {
    this.replying.update((open) => {
      const rest = new Set(open);
      rest.delete(message.id);
      return rest;
    });
  }

  protected onReplySent(message: ContactMessage): void {
    this.closeReply(message);
    this.actionError.set(null);
    this.notice.set(`Resposta enviada a ${message.senderName ?? message.senderEmail}`);
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
