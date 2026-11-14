import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { VehiclePayload } from '../../core/vehicles/vehicles.models';
import { flushPromises } from '../../testing/async';
import {
  alertOf,
  choiceOf,
  choicesOf,
  chooseIn,
  configureDialogTest,
  dialogOf,
  errorsOf,
  fieldOf,
  removeOverlays,
  submitButtonOf,
  submitDialog,
  typeIn,
} from '../../testing/dialog.harness';
import { unit } from '../../testing/units.fixtures';
import { vehicle } from '../../testing/vehicles.fixtures';
import { VehicleDialogConfig, openVehicleDialog } from './vehicle.dialog';

const TAG = 'urban-vehicle-dialog';

type Save = jest.Mock<(payload: VehiclePayload) => Promise<string | null>>;

const FIRST = unit({ block: 'A', number: '101' });
const SECOND = unit({ block: 'B', number: '202' });

function setup(overrides: Partial<VehicleDialogConfig> = {}): {
  save: Save;
  result: Promise<boolean>;
} {
  configureDialogTest();
  const save: Save = jest
    .fn<(payload: VehiclePayload) => Promise<string | null>>()
    .mockResolvedValue(null);
  const result = openVehicleDialog(TestBed.inject(Injector), {
    vehicle: null,
    units: [FIRST, SECOND],
    defaultUnitId: null,
    save,
    ...overrides,
  });
  TestBed.tick();
  return { save, result };
}

async function settle(): Promise<void> {
  await flushPromises();
  TestBed.tick();
  await flushPromises();
  TestBed.tick();
}

function fillValid(): void {
  chooseIn(TAG, 'unitId', SECOND.id);
  typeIn(TAG, 'plate', 'abc-1d23');
  typeIn(TAG, 'model', ' Fiat Argo ');
  typeIn(TAG, 'color', 'Prata');
}

describe('openVehicleDialog', () => {
  afterEach(() => {
    jest.restoreAllMocks();
    removeOverlays();
  });

  describe('novo veículo', () => {
    it('abre em branco, com o título e o botão de cadastro', () => {
      setup();

      expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Novo veículo');
      expect(fieldOf(TAG, 'plate').value).toBe('');
      expect(fieldOf(TAG, 'model').value).toBe('');
      expect(fieldOf(TAG, 'color').value).toBe('');
      expect(choiceOf(TAG, 'unitId')).toBe('');
      expect(submitButtonOf(TAG).textContent?.trim()).toBe('Cadastrar veículo');
      expect(dialogOf(TAG).querySelector('[data-testid="cancel"]')?.textContent?.trim()).toBe(
        'Cancelar',
      );
    });

    it('oferece as unidades ativas, com bloco e número', () => {
      setup();

      expect(choicesOf(TAG, 'unitId').map((option) => option.label)).toEqual([
        'Bloco A · 101',
        'Bloco B · 202',
      ]);
    });

    it('já traz a unidade que a lista está filtrando', () => {
      setup({ defaultUnitId: SECOND.id });

      expect(choiceOf(TAG, 'unitId')).toBe(SECOND.id);
    });

    it('ignora a unidade filtrada que não está entre as ativas', () => {
      setup({ defaultUnitId: 'outra' });

      expect(choiceOf(TAG, 'unitId')).toBe('');
    });

    it('exige unidade, placa, modelo e cor e não chama a API sem eles', async () => {
      const { save } = setup();

      submitDialog(TAG);
      await settle();

      expect(errorsOf(TAG)).toEqual([
        'Escolha a unidade',
        'Informe a placa',
        'Informe o modelo',
        'Informe a cor',
      ]);
      expect(save).not.toHaveBeenCalled();
    });

    it('mostra a placa em maiúsculas e sem pontuação enquanto se digita', () => {
      setup();

      typeIn(TAG, 'plate', 'abc-1d23');

      expect(fieldOf(TAG, 'plate').value).toBe('ABC1D23');
    });

    it.each(['ABC12', 'ABCD123', 'AB1D234'])('recusa a placa "%s"', async (plate) => {
      const { save } = setup();

      fillValid();
      typeIn(TAG, 'plate', plate);
      submitDialog(TAG);
      await settle();

      expect(errorsOf(TAG)).toEqual(['Confira a placa: use o formato ABC1234 ou ABC1D23']);
      expect(save).not.toHaveBeenCalled();
    });

    it('confere o tamanho do modelo e da cor sem contar os espaços das pontas', async () => {
      const { save } = setup();

      fillValid();
      typeIn(TAG, 'model', ' a ');
      typeIn(TAG, 'color', 'x'.repeat(31));
      submitDialog(TAG);
      await settle();

      expect(errorsOf(TAG)).toEqual([
        'O modelo precisa ter ao menos 2 caracteres',
        'A cor pode ter até 30 caracteres',
      ]);
      expect(save).not.toHaveBeenCalled();
    });

    it('envia a placa limpa e o texto sem espaços nas pontas e fecha quando grava', async () => {
      const { save, result } = setup();

      fillValid();
      submitDialog(TAG);
      await settle();

      expect(save).toHaveBeenCalledWith({
        unitId: SECOND.id,
        plate: 'ABC1D23',
        model: 'Fiat Argo',
        color: 'Prata',
      });
      expect(await result).toBe(true);
      expect(document.querySelector(TAG)).toBeNull();
    });

    it('cancelar fecha sem gravar', async () => {
      const { save, result } = setup();

      dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

      expect(await result).toBe(false);
      expect(save).not.toHaveBeenCalled();
    });

    it('mostra o erro da API no diálogo e mantém o que foi digitado', async () => {
      const { save } = setup();
      save.mockResolvedValue('Já existe um veículo com esta placa neste condomínio');

      fillValid();
      submitDialog(TAG);
      await settle();

      expect(alertOf(TAG)).toContain('Já existe um veículo com esta placa');
      expect(document.querySelector(TAG)).not.toBeNull();
      expect(fieldOf(TAG, 'plate').value).toBe('ABC1D23');
      expect(submitButtonOf(TAG).disabled).toBe(false);
    });

    it('não grava de novo enquanto a primeira gravação não terminou', async () => {
      const { save } = setup();
      let finish: (message: string | null) => void = () => undefined;
      save.mockImplementation(() => new Promise((resolve) => (finish = resolve)));

      fillValid();
      submitDialog(TAG);
      submitDialog(TAG);
      await settle();

      expect(save).toHaveBeenCalledTimes(1);
      expect(submitButtonOf(TAG).textContent?.trim()).toBe('Salvando');
      expect(submitButtonOf(TAG).disabled).toBe(true);
      finish(null);
      await settle();
    });
  });

  describe('editar veículo', () => {
    const existing = vehicle({
      unitId: FIRST.id,
      unitBlock: 'A',
      unitNumber: '101',
      plate: 'ABC1D23',
      model: 'Fiat Argo',
      color: 'Prata',
    });

    it('abre preenchido, com o título e o botão de edição desabilitado até mudar algo', () => {
      setup({ vehicle: existing });

      expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Editar veículo');
      expect(choiceOf(TAG, 'unitId')).toBe(FIRST.id);
      expect(fieldOf(TAG, 'plate').value).toBe('ABC1D23');
      expect(fieldOf(TAG, 'model').value).toBe('Fiat Argo');
      expect(fieldOf(TAG, 'color').value).toBe('Prata');
      expect(submitButtonOf(TAG).textContent?.trim()).toBe('Salvar alterações');
      expect(submitButtonOf(TAG).disabled).toBe(true);
    });

    it('habilita ao mudar e volta a desabilitar quando a mudança é desfeita', () => {
      setup({ vehicle: existing });

      typeIn(TAG, 'color', 'Preto');
      TestBed.tick();
      expect(submitButtonOf(TAG).disabled).toBe(false);

      typeIn(TAG, 'color', ' Prata ');
      TestBed.tick();
      expect(submitButtonOf(TAG).disabled).toBe(true);
    });

    it('mudar só a pontuação da placa não conta como mudança', () => {
      setup({ vehicle: existing });

      typeIn(TAG, 'plate', 'abc-1d23');
      TestBed.tick();

      expect(submitButtonOf(TAG).disabled).toBe(true);
    });

    it('grava a mudança e fecha', async () => {
      const { save, result } = setup({ vehicle: existing });

      chooseIn(TAG, 'unitId', SECOND.id);
      typeIn(TAG, 'color', 'Preto');
      submitDialog(TAG);
      await settle();

      expect(save).toHaveBeenCalledWith({
        unitId: SECOND.id,
        plate: 'ABC1D23',
        model: 'Fiat Argo',
        color: 'Preto',
      });
      expect(await result).toBe(true);
    });

    it('não grava sem mudança', async () => {
      const { save } = setup({ vehicle: existing });

      submitDialog(TAG);
      await settle();

      expect(save).not.toHaveBeenCalled();
    });

    it('mantém na lista a unidade desativada onde o veículo está, para poder editar o resto', () => {
      const gone = vehicle({ unitId: 'off', unitBlock: 'C', unitNumber: '303' });
      setup({ vehicle: gone });

      expect(choicesOf(TAG, 'unitId').map((option) => option.label)).toEqual([
        'Bloco A · 101',
        'Bloco B · 202',
        'Bloco C · 303',
      ]);
      expect(choiceOf(TAG, 'unitId')).toBe('off');
    });

    it('não repete a unidade quando ela já está entre as ativas', () => {
      setup({ vehicle: existing });

      expect(choicesOf(TAG, 'unitId')).toHaveLength(2);
    });
  });
});
