import { ChangeDetectionStrategy, Component } from '@angular/core';
import { MuralStore } from './mural.store';

/** O mural de avisos de quem mora no condomínio. */
@Component({
  selector: 'urban-mural',
  template: '',
  providers: [MuralStore],
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class Mural {}
