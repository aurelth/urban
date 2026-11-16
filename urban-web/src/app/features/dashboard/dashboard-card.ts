import { ChangeDetectionStrategy, Component, input, output } from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';
import { SectionStatus } from './dashboard.store';

/**
 * A moldura de um cartão do painel: título, esqueleto enquanto carrega e, se a leitura falha, o aviso com "Tentar de novo".
 * O conteúdo só aparece com a leitura pronta, mas o Angular o cria antes, então quem usa o cartão trata o valor ausente.
 */
@Component({
  selector: 'urban-dashboard-card',
  imports: [MatButton, MatIcon],
  templateUrl: './dashboard-card.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class DashboardCard {
  readonly heading = input.required<string>();
  readonly subtitle = input('');
  readonly status = input.required<SectionStatus>();
  readonly errorText = input('Não foi possível carregar');
  readonly retry = output();
}
