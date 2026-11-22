import { ChangeDetectionStrategy, Component, OnInit, computed, inject } from '@angular/core';
import { MatButton, MatIconButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { categoryClasses } from '../comunicados/announcement-values';
import { MuralCard } from './mural-card';
import { markAllLabel, unreadSummary } from './mural-values';
import { MuralChip, MuralStore } from './mural.store';

const CHIP_BASE =
  'inline-flex min-h-9 cursor-pointer items-center gap-2 rounded-full border px-3.5 py-1 text-sm font-medium transition-colors';
const CHIP_OFF = `${CHIP_BASE} border-outline-variant bg-transparent text-on-surface hover:bg-surface-container`;
const CHIP_ALL_ON = `${CHIP_BASE} border-primary bg-primary text-on-primary`;

/** O mural de avisos de quem mora no condomínio: o que há de novo, por categoria, com a leitura marcada aviso a aviso. */
@Component({
  selector: 'urban-mural',
  imports: [MatButton, MatIconButton, MatIcon, MuralCard],
  providers: [MuralStore],
  templateUrl: './mural.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block mx-auto max-w-3xl p-6' },
})
export class Mural implements OnInit {
  protected readonly store = inject(MuralStore);

  protected readonly unreadSummary = unreadSummary;
  protected readonly markAllLabel = markAllLabel;

  /** Sem nenhum aviso e sem filtro ligado não há o que filtrar: a barra de filtros nem aparece. */
  protected readonly showToolbar = computed(
    () => this.store.total() > 0 || this.store.hasFilters(),
  );
  protected readonly emptyFilterTitle = computed(() =>
    this.store.unreadOnly() && this.store.category() === null
      ? 'Você já leu todos os avisos'
      : 'Nenhum aviso neste filtro',
  );
  protected readonly shownText = computed(() => {
    const total = this.store.total();
    return `Mostrando ${String(this.store.items().length)} de ${String(total)} ${total === 1 ? 'aviso' : 'avisos'}`;
  });

  ngOnInit(): void {
    void this.store.load();
  }

  protected chipClasses(chip: MuralChip): string {
    if (this.store.category() !== chip.value) {
      return CHIP_OFF;
    }
    return chip.value === null
      ? CHIP_ALL_ON
      : `${CHIP_BASE} border-current font-semibold ${categoryClasses(chip.value)}`;
  }
}
