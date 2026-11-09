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
import { openDeleteTariffDialog } from './delete-tariff.dialog';

const TAG = 'urban-delete-tariff-dialog';

type Save = jest.Mock<() => Promise<string | null>>;

function setup(): { save: Save; result: Promise<boolean> } {
  configureDialogTest();
  const save: Save = jest.fn<() => Promise<string | null>>().mockResolvedValue(null);
  const result = openDeleteTariffDialog(TestBed.inject(Injector), { month: '2026-11', save });
  TestBed.tick();
  return { save, result };
}

describe('DeleteTariffDialog', () => {
  afterEach(removeOverlays);

  it('diz qual tarifa vai ser apagada e que os meses fechados nao mudam', () => {
    setup();

    expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe(
      'Apagar a tarifa de novembro de 2026',
    );
    expect(dialogOf(TAG).textContent).toContain('Os meses já fechados não mudam');
    expect(submitButtonOf(TAG).textContent?.trim()).toBe('Apagar tarifa');
  });

  it('apaga e fecha', async () => {
    const { save, result } = setup();

    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledTimes(1);
    expect(await result).toBe(true);
  });

  it('continua aberto com o erro quando a API recusa', async () => {
    const { save } = setup();
    save.mockResolvedValue('Esta tarifa não existe mais');

    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();

    expect(alertOf(TAG)).toBe('Esta tarifa não existe mais');
  });

  it('cancelar fecha sem apagar', async () => {
    const { save, result } = setup();

    dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

    expect(await result).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });
});
