import {
  ChangeDetectionStrategy,
  Component,
  Injector,
  OnInit,
  computed,
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
import { Vehicle, VehiclePayload } from '../../core/vehicles/vehicles.models';
import { MenuOption, MenuSelect } from '../../shared/ui/menu-select/menu-select';
import { unitLabel } from '../units/unit-values';
import { openVehicleDialog } from './vehicle.dialog';
import { VEHICLE_ACTION_MESSAGES, VEHICLE_FORM_MESSAGES } from './vehicles.messages';
import { VehiclesStore } from './vehicles.store';

const ALL_UNITS = '';

/**
 * Lista dos veículos do condomínio: filtro por unidade, busca por placa, modelo ou cor, tabela paginada e as ações de
 * cadastrar, editar e excluir. Excluir pede confirmação na própria linha, porque não se desfaz.
 */
@Component({
  selector: 'urban-vehicles-page',
  imports: [
    RouterLink,
    MatButton,
    MatIconButton,
    MatFormField,
    MatLabel,
    MatIcon,
    MatInput,
    MenuSelect,
  ],
  templateUrl: './vehicles.page.html',
  providers: [VehiclesStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class VehiclesPage implements OnInit {
  protected readonly store = inject(VehiclesStore);
  private readonly injector = inject(Injector);

  protected readonly notice = signal<string | null>(null);
  protected readonly actionError = signal<string | null>(null);
  /** O veículo cuja exclusão está esperando confirmação (um por vez). */
  protected readonly confirming = signal<string | null>(null);

  protected readonly unitOptions = computed<readonly MenuOption[]>(() => [
    { value: ALL_UNITS, label: 'Todas as unidades' },
    ...this.store.units().map((unit) => ({ value: unit.id, label: unitLabel(unit) })),
  ]);

  protected readonly unit = (vehicle: Vehicle): string =>
    unitLabel({ block: vehicle.unitBlock, number: vehicle.unitNumber });

  ngOnInit(): void {
    void this.load();
  }

  protected load(): Promise<void> {
    return this.store.load();
  }

  protected onUnit(value: string): void {
    void this.store.filter({ unitId: value === ALL_UNITS ? null : value });
  }

  protected onSearch(event: Event): void {
    void this.store.filter({ search: (event.target as HTMLInputElement).value });
  }

  protected clearFilters(): void {
    void this.store.filter({ unitId: null, search: '' });
  }

  protected goTo(page: number): void {
    void this.store.goToPage(page);
  }

  protected async create(): Promise<void> {
    this.clearMessages();
    await openVehicleDialog(this.injector, {
      vehicle: null,
      units: this.store.units(),
      defaultUnitId: this.store.query().unitId,
      save: async (payload) => {
        const message = await this.saveAs(this.store.create(payload));
        if (message === null) {
          this.notice.set(`Veículo ${payload.plate} cadastrado`);
        }
        return message;
      },
    });
  }

  protected async edit(target: Vehicle): Promise<void> {
    this.clearMessages();
    await openVehicleDialog(this.injector, {
      vehicle: target,
      units: this.store.units(),
      defaultUnitId: null,
      save: async (payload: VehiclePayload) => {
        const message = await this.saveAs(this.store.update(target, payload));
        if (message === null) {
          this.notice.set(`Veículo ${payload.plate} atualizado`);
        }
        return message;
      },
    });
  }

  protected askToRemove(target: Vehicle): void {
    this.clearMessages();
    this.confirming.set(target.id);
  }

  protected cancelRemoval(): void {
    this.confirming.set(null);
  }

  protected async remove(target: Vehicle): Promise<void> {
    this.clearMessages();
    this.confirming.set(null);
    const problem = await this.store.remove(target);
    if (problem === null) {
      this.notice.set(`Veículo ${target.plate} excluído`);
    } else {
      this.actionError.set(describeProblem(problem, VEHICLE_ACTION_MESSAGES));
    }
  }

  /** Texto do erro para o diálogo, ou nulo quando a gravação deu certo. */
  private async saveAs(request: Promise<ApiProblem | null>): Promise<string | null> {
    const problem = await request;
    return problem === null ? null : describeProblem(problem, VEHICLE_FORM_MESSAGES);
  }

  private clearMessages(): void {
    this.notice.set(null);
    this.actionError.set(null);
  }
}
