import { ChangeDetectionStrategy, Component } from '@angular/core';
import { DelinquencyStore } from './delinquency.store';

/** Inadimplência: as unidades com cobrança vencida e ainda não paga. */
@Component({
  selector: 'urban-delinquency-page',
  template: '',
  providers: [DelinquencyStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class DelinquencyPage {}
