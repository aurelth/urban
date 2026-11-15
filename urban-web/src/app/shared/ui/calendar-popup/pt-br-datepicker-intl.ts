import { Injectable } from '@angular/core';
import { MatDatepickerIntl } from '@angular/material/datepicker';

@Injectable()
export class PtBrDatepickerIntl extends MatDatepickerIntl {
  override calendarLabel = 'Calendário';
  override openCalendarLabel = 'Abrir calendário';
  override closeCalendarLabel = 'Fechar calendário';
  override prevMonthLabel = 'Mês anterior';
  override nextMonthLabel = 'Próximo mês';
  override prevYearLabel = 'Ano anterior';
  override nextYearLabel = 'Próximo ano';
  override prevMultiYearLabel = 'Anteriores 24 anos';
  override nextMultiYearLabel = 'Próximos 24 anos';
  override switchToMonthViewLabel = 'Escolher data';
  override switchToMultiYearViewLabel = 'Escolher mês e ano';

  override formatYearRange(start: string, end: string): string {
    return `${start} – ${end}`;
  }

  override formatYearRangeLabel(start: string, end: string): string {
    return `${start} a ${end}`;
  }
}
