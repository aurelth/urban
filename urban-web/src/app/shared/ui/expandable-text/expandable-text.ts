import { ChangeDetectionStrategy, Component, input } from '@angular/core';

/** Esqueleto: o texto longo com "Ver mais" e "Ver menos" ainda por fazer. */
@Component({
  selector: 'urban-expandable-text',
  template: '',
  changeDetection: ChangeDetectionStrategy.OnPush,
})
export class ExpandableText {
  readonly text = input.required<string>();
}
