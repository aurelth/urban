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
import { RouterLink } from '@angular/router';
import { ApiProblem } from '../../core/http/api-problem';
import { describeProblem } from '../../core/http/problem-message';
import {
  ApportionmentMethod,
  Unit,
  UnitKind,
  UnitPayload,
  UnitStatusFilter,
} from '../../core/units/units.models';
import { MenuOption, MenuSelect } from '../../shared/ui/menu-select/menu-select';
import { ApportionmentCard } from './apportionment.card';
import { openUnitDialog } from './unit.dialog';
import { formatArea, formatPercent, unitLabel } from './unit-values';
import { METHOD_MESSAGES, UNIT_ACTION_MESSAGES, UNIT_FORM_MESSAGES } from './units.messages';
import { UnitsStore } from './units.store';

const STATUSES: readonly UnitStatusFilter[] = ['active', 'deactivated', 'all'];
const KINDS: readonly UnitKind[] = ['apartment', 'house'];

const STATUS_OPTIONS: readonly MenuOption[] = [
  { value: 'active', label: 'Ativas' },
  { value: 'deactivated', label: 'Desativadas' },
  { value: 'all', label: 'Todas' },
];

const TYPE_OPTIONS: readonly MenuOption[] = [
  { value: '', label: 'Todos os tipos' },
  { value: 'apartment', label: 'Apartamentos' },
  { value: 'house', label: 'Casas' },
];

/**
 * Lista das unidades do condomínio: resumo, método de rateio, filtros, tabela paginada e as ações de cadastrar,
 * editar, desativar e reativar. Desativar não pede confirmação porque se desfaz com um clique em "Reativar".
 */
@Component({
  selector: 'urban-units-page',
  imports: [
    RouterLink,
    MatButton,
    MatIconButton,
    MatFormField,
    MatLabel,
    MatIcon,
    MatInput,
    MenuSelect,
    ApportionmentCard,
  ],
  templateUrl: './units.page.html',
  providers: [UnitsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class UnitsPage implements OnInit {
  protected readonly store = inject(UnitsStore);
  private readonly injector = inject(Injector);

  protected readonly notice = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);
  protected readonly methodError = signal<string | null>(null);

  protected readonly statusOptions = STATUS_OPTIONS;
  protected readonly typeOptions = TYPE_OPTIONS;

  protected readonly label = unitLabel;
  protected readonly percent = formatPercent;
  protected readonly area = formatArea;

  ngOnInit(): void {
    void this.load();
  }

  protected load(): Promise<void> {
    return this.store.load();
  }

  protected onStatus(value: string): void {
    const status = STATUSES.find((candidate) => candidate === value) ?? 'active';
    void this.store.filter({ status });
  }

  protected onType(value: string): void {
    void this.store.filter({ type: KINDS.find((candidate) => candidate === value) ?? null });
  }

  protected onBlock(event: Event): void {
    void this.store.filter({ block: fieldValue(event) });
  }

  protected onSearch(event: Event): void {
    void this.store.filter({ search: fieldValue(event) });
  }

  protected clearFilters(): void {
    void this.store.filter({ status: 'active', type: null, block: '', search: '' });
  }

  protected goTo(page: number): void {
    void this.store.goToPage(page);
  }

  protected async create(): Promise<void> {
    this.clearMessages();
    await openUnitDialog(this.injector, {
      unit: null,
      save: async (payload) => {
        const message = await this.saveAs(this.store.create(payload));
        if (message === null) {
          this.notice.set(`${unitLabel(payload)} cadastrada`);
        }
        return message;
      },
    });
  }

  protected async edit(target: Unit): Promise<void> {
    this.clearMessages();
    await openUnitDialog(this.injector, {
      unit: target,
      save: async (payload: UnitPayload) => {
        const message = await this.saveAs(this.store.update(target, payload));
        if (message === null) {
          this.notice.set(`${unitLabel(payload)} atualizada`);
        }
        return message;
      },
    });
  }

  protected async deactivate(target: Unit): Promise<void> {
    await this.changeStatus(this.store.deactivate(target), `${unitLabel(target)} desativada`);
  }

  protected async reactivate(target: Unit): Promise<void> {
    await this.changeStatus(this.store.reactivate(target), `${unitLabel(target)} reativada`);
  }

  protected async changeMethod(method: ApportionmentMethod): Promise<void> {
    this.methodError.set(null);
    const problem = await this.store.changeMethod(method);
    if (problem !== null) {
      this.methodError.set(describeProblem(problem, METHOD_MESSAGES));
    }
  }

  private async changeStatus(request: Promise<ApiProblem | null>, success: string): Promise<void> {
    this.clearMessages();
    const problem = await request;
    if (problem === null) {
      this.notice.set(success);
    } else {
      this.actionError.set(describeProblem(problem, UNIT_ACTION_MESSAGES));
    }
  }

  /** Texto do erro para o diálogo, ou nulo quando a gravação deu certo. */
  private async saveAs(request: Promise<ApiProblem | null>): Promise<string | null> {
    const problem = await request;
    return problem === null ? null : describeProblem(problem, UNIT_FORM_MESSAGES);
  }

  private clearMessages(): void {
    this.notice.set(null);
    this.actionError.set(null);
  }
}

function fieldValue(event: Event): string {
  return (event.target as HTMLInputElement | HTMLSelectElement).value;
}
