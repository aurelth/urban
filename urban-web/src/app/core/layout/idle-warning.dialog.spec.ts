import { Injector, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { MATERIAL_ANIMATIONS } from '@angular/material/core';
import { jest } from '@jest/globals';
import { flushPromises } from '../../testing/async';
import { openIdleWarning } from './idle-warning.dialog';

function setup(seconds = 45): {
  secondsLeft: ReturnType<typeof signal<number>>;
  keep: jest.Mock<() => void>;
  warning: ReturnType<typeof openIdleWarning>;
} {
  TestBed.configureTestingModule({
    providers: [{ provide: MATERIAL_ANIMATIONS, useValue: { animationsDisabled: true } }],
  });
  const secondsLeft = signal(seconds);
  const keep = jest.fn<() => void>();
  const warning = openIdleWarning(TestBed.inject(Injector), secondsLeft, keep);
  return { secondsLeft, keep, warning };
}

function dialog(): HTMLElement | null {
  return document.querySelector('urban-idle-warning-dialog');
}

describe('openIdleWarning', () => {
  afterEach(() => {
    document.querySelectorAll('.cdk-overlay-container').forEach((overlay) => overlay.remove());
  });

  it('pergunta se a pessoa ainda esta ali e mostra os segundos que faltam', () => {
    const { secondsLeft } = setup(45);
    TestBed.tick();

    expect(dialog()?.querySelector('h2')?.textContent?.trim()).toBe('Você ainda está aí?');
    expect(dialog()?.textContent).toContain('em 45 segundos');

    secondsLeft.set(1);
    TestBed.tick();

    expect(dialog()?.textContent).toContain('em 1 segundo');
    expect(dialog()?.textContent).not.toContain('1 segundos');
  });

  it('e um aviso modal que nao fecha ao clicar fora', () => {
    setup();
    TestBed.tick();

    const container = document.querySelector('mat-dialog-container');
    expect(container?.getAttribute('role')).toBe('alertdialog');
    document.querySelector<HTMLElement>('.cdk-overlay-backdrop')?.click();
    TestBed.tick();

    expect(dialog()).not.toBeNull();
  });

  it('continuar conectado fecha o aviso e avisa quem abriu', async () => {
    const { keep } = setup();
    TestBed.tick();

    document.querySelector<HTMLButtonElement>('button')!.click();
    await flushPromises();
    TestBed.tick();

    expect(keep).toHaveBeenCalledTimes(1);
    expect(dialog()).toBeNull();
  });

  it('fechar por fora tira o aviso sem contar como atividade', async () => {
    const { keep, warning } = setup();
    TestBed.tick();

    warning.close();
    await flushPromises();
    TestBed.tick();

    expect(keep).not.toHaveBeenCalled();
    expect(dialog()).toBeNull();
  });
});
