import {
  ChangeDetectionStrategy,
  Component,
  DestroyRef,
  ElementRef,
  afterNextRender,
  afterRenderEffect,
  inject,
  input,
  linkedSignal,
  signal,
  viewChild,
} from '@angular/core';
import { MatButton } from '@angular/material/button';
import { MatIcon } from '@angular/material/icon';

/**
 * Texto longo que mostra até 4 linhas, com "…" no corte, e só oferece "Ver mais" quando o texto de fato passa disso.
 * "Ver mais" abre o texto inteiro e "Ver menos" volta às 4 linhas. As quebras de linha do texto são mantidas.
 */
@Component({
  selector: 'urban-expandable-text',
  imports: [MatButton, MatIcon],
  templateUrl: './expandable-text.html',
  changeDetection: ChangeDetectionStrategy.OnPush,
  host: { class: 'block' },
})
export class ExpandableText {
  readonly text = input.required<string>();

  /** Texto novo (uma edição) volta a mostrar só o começo. */
  protected readonly expanded = linkedSignal<string, boolean>({
    source: this.text,
    computation: () => false,
  });
  protected readonly overflowing = signal(false);

  private readonly paragraph = viewChild.required<ElementRef<HTMLElement>>('paragraph');

  constructor() {
    const destroyRef = inject(DestroyRef);
    afterRenderEffect(() => {
      this.text();
      this.measure();
    });
    afterNextRender(() => {
      if (typeof ResizeObserver === 'undefined') {
        return;
      }
      const observer = new ResizeObserver(() => this.measure());
      observer.observe(this.paragraph().nativeElement);
      destroyRef.onDestroy(() => observer.disconnect());
    });
  }

  protected toggle(): void {
    this.expanded.update((open) => !open);
  }

  /** Só dá para saber se o texto passa de 4 linhas com o corte aplicado; aberto, o que já foi visto vale. */
  private measure(): void {
    if (this.expanded()) {
      return;
    }
    const element = this.paragraph().nativeElement;
    this.overflowing.set(element.scrollHeight > element.clientHeight);
  }
}
