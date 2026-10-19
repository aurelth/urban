import {
  DOCUMENT,
  DestroyRef,
  Directive,
  ElementRef,
  afterNextRender,
  inject,
  input,
} from '@angular/core';
import { gsap } from 'gsap';

/**
 * Entrada suave (fade + subida) com GSAP. Uso: <div urbanFadeIn [urbanFadeInDelay]="0.1">.
 * Nao anima quando o usuario prefere menos movimento.
 */
@Directive({ selector: '[urbanFadeIn]' })
export class FadeInDirective {
  readonly delay = input(0, { alias: 'urbanFadeInDelay' });

  private readonly element = inject<ElementRef<HTMLElement>>(ElementRef).nativeElement;
  private readonly window = inject(DOCUMENT).defaultView;

  constructor() {
    afterNextRender(() => {
      if (this.prefersReducedMotion()) {
        return;
      }
      gsap.from(this.element, {
        opacity: 0,
        y: 16,
        duration: 0.5,
        ease: 'power2.out',
        delay: this.delay(),
        clearProps: 'all',
      });
    });
    inject(DestroyRef).onDestroy(() => gsap.killTweensOf(this.element));
  }

  private prefersReducedMotion(): boolean {
    return this.window?.matchMedia?.('(prefers-reduced-motion: reduce)').matches ?? false;
  }
}
