import {
  ChangeDetectionStrategy,
  Component,
  afterNextRender,
  input,
  output,
  viewChild,
} from '@angular/core';
import { MAT_DATE_LOCALE, provideNativeDateAdapter } from '@angular/material/core';
import { MatDatepicker, MatDatepickerInput, MatDatepickerIntl } from '@angular/material/datepicker';
import { PtBrDatepickerIntl } from './pt-br-datepicker-intl';

/** Anos atrás em que o calendário de nascimento abre quando o campo está vazio. */
const BIRTH_START_YEARS_AGO = 30;

/**
 * Calendário em português que abre sozinho ao ser criado, ancorado ao campo de data que o contém.
 * Não é um campo: quem digita a data é o `urban-text-field`; este componente só escolhe um dia e
 * devolve a data, e é carregado sob demanda (`@defer`) para o Material Datepicker ficar fora do
 * pacote inicial. O `input` escondido existe só porque o Material ancora o calendário num campo.
 */
@Component({
  selector: 'urban-calendar-popup',
  imports: [MatDatepicker, MatDatepickerInput],
  templateUrl: './calendar.popup.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  providers: [
    provideNativeDateAdapter(),
    { provide: MAT_DATE_LOCALE, useValue: 'pt-BR' },
    { provide: MatDatepickerIntl, useClass: PtBrDatepickerIntl },
  ],
  host: { class: 'absolute inset-x-0 bottom-0 block h-0' },
})
export class CalendarPopup {
  readonly value = input<Date | null>(null);
  /** Data de nascimento: abre na lista de anos, onde se chega a 1985 em dois toques. */
  readonly birthDate = input(false);
  readonly picked = output<Date>();
  readonly closed = output<void>();

  protected readonly picker = viewChild.required<MatDatepicker<Date>>('picker');

  constructor() {
    afterNextRender(() => this.picker().open());
  }

  protected pick(date: Date | null): void {
    if (date !== null) {
      this.picked.emit(date);
    }
  }

  protected startAt(): Date {
    const value = this.value();
    if (value !== null || !this.birthDate()) {
      return value ?? new Date();
    }
    const start = new Date();
    start.setFullYear(start.getFullYear() - BIRTH_START_YEARS_AGO);
    return start;
  }

  protected startView(): 'month' | 'multi-year' {
    return this.birthDate() ? 'multi-year' : 'month';
  }
}
