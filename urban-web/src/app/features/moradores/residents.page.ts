import { NgTemplateOutlet } from '@angular/common';
import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatFormField, MatLabel } from '@angular/material/form-field';
import { MatIcon } from '@angular/material/icon';
import { MatInput } from '@angular/material/input';
import { RouterLink } from '@angular/router';
import { ResidentStatusFilter } from '../../core/residents/residents.models';
import { MenuOption, MenuSelect } from '../../shared/ui/menu-select/menu-select';
import {
  formatPhone,
  isCurrentResident,
  kindLabel,
  occupancyPeriod,
  occupancyUnit,
} from './resident-values';
import { ResidentsStore } from './residents.store';

const STATUSES: readonly ResidentStatusFilter[] = ['active', 'former', 'all'];

const STATUS_OPTIONS: readonly MenuOption[] = [
  { value: 'active', label: 'Moradores atuais' },
  { value: 'former', label: 'Ex-moradores' },
  { value: 'all', label: 'Todos' },
];

/**
 * Lista dos moradores do condomínio: busca por nome, filtro entre quem mora e quem já saiu, e uma linha por pessoa com o
 * contato e todos os vínculos (unidade, tipo e período). Daqui se abre o cadastro ("Novo morador") e a edição de cada morador.
 */
@Component({
  selector: 'urban-residents-page',
  imports: [
    MatButton,
    MatFormField,
    MatLabel,
    MatIcon,
    MatInput,
    MenuSelect,
    NgTemplateOutlet,
    RouterLink,
  ],
  templateUrl: './residents.page.html',
  providers: [ResidentsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ResidentsPage implements OnInit {
  protected readonly store = inject(ResidentsStore);

  protected readonly statusOptions = STATUS_OPTIONS;

  protected readonly lives = isCurrentResident;
  protected readonly unit = occupancyUnit;
  protected readonly kind = kindLabel;
  protected readonly period = occupancyPeriod;
  protected readonly phone = formatPhone;

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

  protected onSearch(event: Event): void {
    void this.store.filter({ search: (event.target as HTMLInputElement).value });
  }

  protected clearFilters(): void {
    void this.store.filter({ status: 'active', search: '' });
  }

  protected goTo(page: number): void {
    void this.store.goToPage(page);
  }
}
