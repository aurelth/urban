import { Component } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { gsap } from 'gsap';
import { FadeInDirective } from './fade-in.directive';

@Component({
  imports: [FadeInDirective],
  template: `<div urbanFadeIn [urbanFadeInDelay]="0.2">conteudo</div>`,
})
class HostComponent {}

function mockMatchMedia(reduceMotion: boolean): void {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: () => ({ matches: reduceMotion }),
  });
}

describe('FadeInDirective', () => {
  afterEach(() => jest.restoreAllMocks());

  it('anima a entrada do elemento com GSAP', async () => {
    mockMatchMedia(false);
    const from = jest.spyOn(gsap, 'from').mockReturnValue({} as gsap.core.Tween);

    const fixture = TestBed.createComponent(HostComponent);
    await fixture.whenStable();

    expect(from).toHaveBeenCalledTimes(1);
    expect(from.mock.calls[0][1]).toEqual(expect.objectContaining({ opacity: 0, delay: 0.2 }));
  });

  it('nao anima quando o usuario prefere menos movimento', async () => {
    mockMatchMedia(true);
    const from = jest.spyOn(gsap, 'from');

    const fixture = TestBed.createComponent(HostComponent);
    await fixture.whenStable();

    expect(from).not.toHaveBeenCalled();
  });
});
