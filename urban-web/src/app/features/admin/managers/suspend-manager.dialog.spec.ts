import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { flushPromises } from '../../../testing/async';
import { manager } from '../../../testing/admin.fixtures';
import {
  alertOf,
  configureDialogTest,
  dialogOf,
  errorsOf,
  removeOverlays,
  submitButtonOf,
  submitDialog,
  typeIn,
} from '../../../testing/dialog.harness';
import { SuspendManagerDialogConfig, openSuspendManagerDialog } from './suspend-manager.dialog';

const TAG = 'urban-suspend-manager-dialog';

type Save = jest.Mock<(reason: string) => Promise<string | null>>;

function setup(name: string | null = 'Ana'): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  const save: Save = jest.fn<(reason: string) => Promise<string | null>>().mockResolvedValue(null);
  const config: SuspendManagerDialogConfig = { manager: manager({ name }), save };
  const result = openSuspendManagerDialog(TestBed.inject(Injector), config);
  TestBed.tick();
  return { save, result };
}

async function settle(): Promise<void> {
  await flushPromises();
  TestBed.tick();
  await flushPromises();
  TestBed.tick();
}

describe('openSuspendManagerDialog', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    removeOverlays();
  });

  it('abre com o nome no titulo, a explicacao e o campo do motivo em branco', () => {
    setup();

    expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Suspender Ana');
    expect(dialogOf(TAG).textContent).toContain('Você pode reativar a conta quando quiser');
    expect(dialogOf(TAG).textContent).toContain('sessões abertas');
    expect(
      dialogOf(TAG).querySelector<HTMLTextAreaElement>('textarea[formControlName="reason"]')!.value,
    ).toBe('');
    expect(submitButtonOf(TAG).textContent?.trim()).toBe('Suspender acesso');
  });

  it('usa o e-mail no titulo quando o sindico ainda nao tem nome', () => {
    setup(null);

    expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe(
      'Suspender ana@example.com',
    );
  });

  it('nao grava sem motivo e pede que seja informado', async () => {
    const { save } = setup();

    submitDialog(TAG);
    await settle();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual(['Informe o motivo da suspensão']);
  });

  it('trata motivo so de espacos como vazio', async () => {
    const { save } = setup();

    typeIn(TAG, 'reason', '    ');
    submitDialog(TAG);
    await settle();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual(['Informe o motivo da suspensão']);
  });

  it('nao aceita motivo com mais de 500 caracteres', async () => {
    const { save } = setup();

    typeIn(TAG, 'reason', 'a'.repeat(501));
    submitDialog(TAG);
    await settle();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual(['O motivo pode ter até 500 caracteres']);
  });

  it('grava o motivo sem espacos nas pontas e fecha, respondendo verdadeiro', async () => {
    const { save, result } = setup();

    typeIn(TAG, 'reason', '  Uso indevido da plataforma ');
    submitDialog(TAG);
    await settle();

    expect(save).toHaveBeenCalledWith('Uso indevido da plataforma');
    expect(document.querySelector(TAG)).toBeNull();
    await expect(result).resolves.toBe(true);
  });

  it('aceita um motivo com exatamente 500 caracteres', async () => {
    const { save } = setup();

    typeIn(TAG, 'reason', 'a'.repeat(500));
    submitDialog(TAG);
    await settle();

    expect(save).toHaveBeenCalledWith('a'.repeat(500));
  });

  it('trava os botoes enquanto grava', async () => {
    const { save } = setup();
    let finish: (message: string | null) => void = () => undefined;
    save.mockReturnValue(new Promise((resolve) => (finish = resolve)));

    typeIn(TAG, 'reason', 'Motivo');
    submitDialog(TAG);
    TestBed.tick();

    expect(submitButtonOf(TAG).disabled).toBe(true);
    expect(dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.disabled).toBe(
      true,
    );
    finish(null);
    await settle();
  });

  it('explica no dialogo por que a API recusou, sem fechar e sem perder o motivo', async () => {
    const { save } = setup();
    save.mockResolvedValue('A situação deste síndico mudou. A lista foi atualizada');

    typeIn(TAG, 'reason', 'Motivo');
    submitDialog(TAG);
    await settle();

    expect(alertOf(TAG)).toBe('A situação deste síndico mudou. A lista foi atualizada');
    expect(document.querySelector(TAG)).not.toBeNull();
    expect(dialogOf(TAG).querySelector<HTMLTextAreaElement>('textarea')!.value).toBe('Motivo');
    expect(submitButtonOf(TAG).disabled).toBe(false);
  });

  it('cancelar fecha sem gravar, respondendo falso', async () => {
    const { save, result } = setup();

    dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();
    await settle();

    expect(save).not.toHaveBeenCalled();
    expect(document.querySelector(TAG)).toBeNull();
    await expect(result).resolves.toBe(false);
  });
});
