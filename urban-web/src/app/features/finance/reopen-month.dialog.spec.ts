import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { flushPromises } from '../../testing/async';
import {
  alertOf,
  configureDialogTest,
  dialogOf,
  removeOverlays,
  submitButtonOf,
  submitDialog,
} from '../../testing/dialog.harness';
import { openReopenMonthDialog } from './reopen-month.dialog';

const TAG = 'urban-reopen-month-dialog';

type Save = jest.Mock<() => Promise<string | null>>;

function setup(): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  const save: Save = jest.fn<() => Promise<string | null>>().mockResolvedValue(null);
  const result = openReopenMonthDialog(TestBed.inject(Injector), { month: '2026-11', save });
  TestBed.tick();
  return { save, result };
}

describe('ReopenMonthDialog', () => {
  afterEach(removeOverlays);

  it('avisa que as cobrancas serao apagadas e refeitas ao fechar de novo', () => {
    setup();

    expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Reabrir Novembro de 2026');
    const text = dialogOf(TAG).textContent ?? '';
    expect(text).toContain('As cobranças deste mês serão apagadas');
    expect(text).toContain('voltam a poder ser editados');
    expect(text).toContain('Ao fechar de novo, as cobranças são calculadas outra vez');
    expect(submitButtonOf(TAG).textContent?.trim()).toBe('Reabrir mês');
  });

  it('reabre ao confirmar', async () => {
    const { save, result } = setup();

    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledTimes(1);
    expect(await result).toBe(true);
  });

  it('continua aberto com o erro quando a API recusa', async () => {
    const { save } = setup();
    save.mockResolvedValue('Este mês não está fechado');

    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();

    expect(alertOf(TAG)).toBe('Este mês não está fechado');
  });

  it('cancelar fecha sem reabrir', async () => {
    const { save, result } = setup();

    dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

    expect(await result).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });
});
