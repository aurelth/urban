import { WritableSignal, signal } from '@angular/core';
import { addMonths, isMonthInWindow } from '../../core/finance/month';
import { initialMonth } from './readings-values';

/**
 * A navegação entre meses de uma tela: começa no mês pedido no endereço (`?mes=AAAA-MM`, se valer) ou no de hoje, anda de
 * mês em mês dentro da janela das telas do Financeiro e chama `load` a cada mês que a tela passa a mostrar.
 */
export class MonthNavigation {
  private readonly current: WritableSignal<string>;

  constructor(
    requested: string | null,
    private readonly now: () => Date,
    private readonly load: (month: string) => Promise<void>,
  ) {
    this.current = signal(initialMonth(requested, now()));
  }

  readonly month = (): string => this.current();

  start(): Promise<void> {
    return this.load(this.current());
  }

  canGo(delta: number): boolean {
    return isMonthInWindow(addMonths(this.current(), delta), this.now());
  }

  go(delta: number): Promise<void> {
    return this.show(addMonths(this.current(), delta));
  }

  reload(): Promise<void> {
    return this.show(this.current());
  }

  private show(month: string): Promise<void> {
    this.current.set(month);
    return this.load(month);
  }
}
