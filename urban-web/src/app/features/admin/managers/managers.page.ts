import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  OnInit,
  inject,
  signal,
} from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { Manager, ManagerStatus, ManagerStatusFilter } from '../../../core/admin/admin.models';
import { describeProblem } from '../../../core/http/problem-message';
import { MenuOption, MenuSelect } from '../../../shared/ui/menu-select/menu-select';
import { formatDay } from '../admin-dates';
import { managerLabel } from './manager-name';
import { MANAGER_MESSAGES } from './managers.messages';
import { ManagersStore } from './managers.store';
import { openSuspendManagerDialog } from './suspend-manager.dialog';

const STATUS_OPTIONS: readonly MenuOption[] = [
  { value: '', label: 'Todos' },
  { value: 'active', label: 'Ativos' },
  { value: 'suspended', label: 'Suspensos' },
  { value: 'deactivated', label: 'Desativados' },
  { value: 'pendingVerification', label: 'Aguardando confirmação' },
];

const FILTERS: readonly ManagerStatusFilter[] = STATUS_OPTIONS.map(
  (option) => option.value as ManagerStatusFilter,
);

/** Texto e ícone de cada situação: o estado nunca depende só de cor. */
const STATUS_VIEW: Readonly<
  Record<ManagerStatus, { label: string; icon: string; tone: 'success' | 'error' | 'neutral' }>
> = {
  Active: { label: 'Ativo', icon: 'check_circle', tone: 'success' },
  Suspended: { label: 'Suspenso', icon: 'block', tone: 'error' },
  Deactivated: { label: 'Desativado', icon: 'pause_circle', tone: 'neutral' },
  PendingVerification: {
    label: 'Aguardando confirmação',
    icon: 'mark_email_unread',
    tone: 'neutral',
  },
};

/**
 * Lista dos síndicos da plataforma: busca, filtro de situação, tabela paginada e as ações de suspender (com motivo) e
 * reativar. Reativar não pede confirmação porque se desfaz suspendendo de novo.
 */
@Component({
  selector: 'urban-managers-page',
  imports: [MatButton, MatIconButton, MatFormField, MatLabel, MatIcon, MatInput, MenuSelect],
  templateUrl: './managers.page.html',
  providers: [ManagersStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ManagersPage implements OnInit {
  protected readonly store = inject(ManagersStore);
  private readonly injector = inject(Injector);

  protected readonly notice = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);

  protected readonly statusOptions = STATUS_OPTIONS;
  protected readonly label = managerLabel;
  protected readonly formatDay = formatDay;
  protected readonly view = (status: ManagerStatus) => STATUS_VIEW[status];

  /** Só quem tem a conta ativa ou desativada pode ser suspenso; só o suspenso pode ser reativado. */
  protected canSuspend(manager: Manager): boolean {
    return manager.status === 'Active' || manager.status === 'Deactivated';
  }

  ngOnInit(): void {
    void this.load();
  }

  protected load(): Promise<void> {
    return this.store.load();
  }

  protected onStatus(value: string): void {
    void this.store.filter({ status: FILTERS.find((candidate) => candidate === value) ?? '' });
  }

  protected onSearch(event: Event): void {
    void this.store.filter({ search: (event.target as HTMLInputElement).value });
  }

  protected clearFilters(): void {
    void this.store.filter({ status: '', search: '' });
  }

  protected goTo(page: number): void {
    void this.store.goToPage(page);
  }

  protected async suspend(target: Manager): Promise<void> {
    this.clearMessages();
    await openSuspendManagerDialog(this.injector, {
      manager: target,
      save: async (reason) => {
        const problem = await this.store.suspend(target, reason);
        if (problem === null) {
          this.notice.set(`Acesso de ${managerLabel(target)} suspenso`);
          return null;
        }
        return describeProblem(problem, MANAGER_MESSAGES);
      },
    });
  }

  protected async reactivate(target: Manager): Promise<void> {
    this.clearMessages();
    const problem = await this.store.reactivate(target);
    if (problem === null) {
      this.notice.set(`Acesso de ${managerLabel(target)} reativado`);
    } else {
      this.actionError.set(describeProblem(problem, MANAGER_MESSAGES));
    }
  }

  private clearMessages(): void {
    this.notice.set(null);
    this.actionError.set(null);
  }
}
