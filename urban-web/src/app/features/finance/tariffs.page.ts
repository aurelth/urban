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
import { MatIcon } from '@angular/material/icon';
import { RouterLink } from '@angular/router';
import { NOW } from '../../core/finance/clock';
import { ConsumptionTariff } from '../../core/finance/finance.models';
import { currentMonth } from '../../core/finance/month';
import { ApiProblem } from '../../core/http/api-problem';
import { describeProblem } from '../../core/http/problem-message';
import { openDeleteTariffDialog } from './delete-tariff.dialog';
import { FINANCE_FORM_MESSAGES } from './finance.messages';
import { openReservePercentDialog } from './reserve-percent.dialog';
import { openTariffDialog } from './tariff.dialog';
import { inForceMonth, monthName, reserveFundText, tariffLines } from './tariff-values';
import { TariffsStore } from './tariffs.store';

/**
 * Tarifas de consumo do condomínio (água, esgoto e gás) e o percentual do fundo de reserva. Cada tarifa vale a partir de um mês,
 * até a próxima começar; toda gravação acontece num diálogo, que fica aberto com o erro até dar certo.
 */
@Component({
  selector: 'urban-tariffs-page',
  imports: [RouterLink, MatButton, MatIconButton, MatIcon],
  templateUrl: './tariffs.page.html',
  providers: [TariffsStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class TariffsPage implements OnInit {
  protected readonly store = inject(TariffsStore);
  private readonly injector = inject(Injector);
  private readonly now = inject(NOW);

  protected readonly notice = signal<string | null>(null);

  /** O mês de início da tarifa que vale hoje, para marcá-la na lista. */
  protected readonly inForce = computed(() =>
    inForceMonth(this.store.tariffs(), currentMonth(this.now())),
  );

  protected readonly name = monthName;
  protected readonly lines = tariffLines;
  protected readonly reserveText = reserveFundText;

  ngOnInit(): void {
    void this.load();
  }

  protected load(): Promise<void> {
    return this.store.load();
  }

  protected async create(): Promise<void> {
    await this.openEditor(null);
  }

  protected async edit(target: ConsumptionTariff): Promise<void> {
    await this.openEditor(target);
  }

  protected async remove(target: ConsumptionTariff): Promise<void> {
    this.notice.set(null);
    await openDeleteTariffDialog(this.injector, {
      month: target.validFrom,
      save: () => this.saved(this.store.deleteTariff(target.validFrom), 'Tarifa apagada'),
    });
  }

  protected async editReservePercent(): Promise<void> {
    this.notice.set(null);
    await openReservePercentDialog(this.injector, {
      percent: this.store.reserveFundPercent(),
      save: (percent) =>
        this.saved(
          this.store.setReserveFundPercent(percent),
          'Percentual do fundo de reserva atualizado',
        ),
    });
  }

  private async openEditor(target: ConsumptionTariff | null): Promise<void> {
    this.notice.set(null);
    await openTariffDialog(this.injector, {
      tariff: target,
      takenMonths: this.store.tariffs().map((tariff) => tariff.validFrom),
      month: currentMonth(this.now()),
      save: (month, payload) => this.saved(this.store.saveTariff(month, payload), 'Tarifa salva'),
    });
  }

  /** Texto do erro para o diálogo, ou nulo quando a gravação deu certo (e então o aviso aparece na tela). */
  private async saved(
    request: Promise<ApiProblem | null>,
    success: string,
  ): Promise<string | null> {
    const problem = await request;
    if (problem === null) {
      this.notice.set(success);
      return null;
    }
    return describeProblem(problem, FINANCE_FORM_MESSAGES);
  }
}
