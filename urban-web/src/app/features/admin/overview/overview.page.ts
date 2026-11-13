import { ChangeDetectionStrategy, Component, OnInit, inject } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { OverviewStore } from './overview.store';

/** Primeira tela do administrador: os números da plataforma e atalhos para o que pede atenção. */
@Component({
  selector: 'urban-overview-page',
  imports: [RouterLink, MatButton, MatIcon],
  templateUrl: './overview.page.html',
  providers: [OverviewStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class OverviewPage implements OnInit {
  protected readonly store = inject(OverviewStore);

  ngOnInit(): void {
    void this.load();
  }

  protected load(): Promise<void> {
    return this.store.load();
  }

  protected pendingLabel(count: number): string {
    if (count === 0) {
      return 'Nenhum pedido aguardando análise';
    }
    return `${count} ${count === 1 ? 'pedido aguardando' : 'pedidos aguardando'} análise`;
  }
}
