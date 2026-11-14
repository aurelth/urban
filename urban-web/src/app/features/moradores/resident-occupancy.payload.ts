import {
  EndOccupancyPayload,
  OccupancyKind,
  OccupancyPayload,
} from '../../core/residents/residents.models';
import { unmask } from '../../shared/forms/masks';

/** Os campos do formulário de nova unidade, como aparecem na tela (data em dd/mm/aaaa). */
export interface OccupancyFormValue {
  readonly unitId: string;
  readonly kind: OccupancyKind | '';
  readonly startDate: string;
}

/** O corpo do novo vínculo: o formulário já conferiu que unidade e tipo foram escolhidos. */
export function toOccupancyPayload(value: OccupancyFormValue): OccupancyPayload {
  return {
    unitId: value.unitId,
    kind: value.kind as OccupancyKind,
    startDate: unmask('date', value.startDate),
  };
}

/** O corpo da saída: o dia digitado em dd/mm/aaaa vira AAAA-MM-DD. */
export function toEndPayload(endDate: string): EndOccupancyPayload {
  return { endDate: unmask('date', endDate) };
}
