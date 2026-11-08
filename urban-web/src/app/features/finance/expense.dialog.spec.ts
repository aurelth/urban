import { Injector } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { jest } from '@jest/globals';
import { ExpensePayload } from '../../core/finance/finance.models';
import { flushPromises } from '../../testing/async';
import {
  alertOf,
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
import { expense, expenseGroup } from '../../testing/finance.fixtures';
import { openExpenseDialog } from './expense.dialog';

const TAG = 'urban-expense-dialog';
const ordinary = expenseGroup({ id: '0199bbbb-0001-7000-8000-000000000001' });
const works = expenseGroup({ id: '0199bbbb-0001-7000-8000-000000000002', name: 'Obras' });

type Save = jest.Mock<(payload: ExpensePayload) => Promise<string | null>>;

function setup(current: ReturnType<typeof expense> | null = null): {
  save: Save;
  result: Promise<boolean>;
} {
  configureDialogTest();
  const save: Save = jest
    .fn<(payload: ExpensePayload) => Promise<string | null>>()
    .mockResolvedValue(null);
  const result = openExpenseDialog(TestBed.inject(Injector), {
    expense: current,
    groups: [ordinary, works],
    month: '2026-11',
    save,
  });
  TestBed.tick();
  return { save, result };
}

function has(name: string): boolean {
  return dialogOf(TAG).querySelector(`[formControlName="${name}"]`) !== null;
}

function chooseKind(kind: string): void {
  chooseIn(TAG, 'kind', kind);
  TestBed.tick();
}

describe('ExpenseDialog (nova despesa)', () => {
  afterEach(removeOverlays);

  it('abre como despesa fixa mensal, no mes atual e no primeiro grupo', () => {
    setup();

    expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Nova despesa');
    expect(fieldOf(TAG, 'kind').value).toBe('fixed');
    expect(fieldOf(TAG, 'groupId').value).toBe(ordinary.id);
    expect(fieldOf(TAG, 'startMonth').value).toBe('2026-11');
    expect(fieldOf(TAG, 'endMonth').value).toBe('');
    expect(submitButtonOf(TAG).textContent?.trim()).toBe('Cadastrar despesa');
  });

  it('oferece tres anos para tras e dois para a frente no mes inicial', () => {
    setup();

    const months = [...fieldOf(TAG, 'startMonth').options].map((option) => option.value);

    expect(months[0]).toBe('2023-11');
    expect(months.at(-1)).toBe('2028-11');
    expect(months).toHaveLength(61);
  });

  it('mostra os campos de cada tipo', () => {
    setup();

    expect(['amount', 'endMonth', 'installmentCount', 'currentInstallment'].map(has)).toEqual([
      true,
      true,
      false,
      false,
    ]);

    chooseKind('variable');
    expect(['amount', 'endMonth', 'installmentCount', 'currentInstallment'].map(has)).toEqual([
      false,
      true,
      false,
      false,
    ]);

    chooseKind('single');
    expect(['amount', 'endMonth', 'installmentCount', 'currentInstallment'].map(has)).toEqual([
      true,
      false,
      false,
      false,
    ]);

    chooseKind('installment');
    expect(['amount', 'endMonth', 'installmentCount', 'currentInstallment'].map(has)).toEqual([
      true,
      false,
      true,
      true,
    ]);
    expect(fieldOf(TAG, 'currentInstallment').value).toBe('1');
  });

  it('explica o tipo escolhido', () => {
    setup();
    expect(dialogOf(TAG).textContent).toContain('Mesmo valor todo mês');

    chooseKind('variable');
    expect(dialogOf(TAG).textContent).toContain('O valor é informado a cada mês');
  });

  it('cadastra despesa fixa com fornecedor e mes final', async () => {
    const { save, result } = setup();

    chooseIn(TAG, 'groupId', works.id);
    typeIn(TAG, 'description', '  Portaria  ');
    typeIn(TAG, 'supplier', ' Segura Ltda ');
    chooseIn(TAG, 'startMonth', '2026-09');
    chooseIn(TAG, 'endMonth', '2027-08');
    typeIn(TAG, 'amount', '4.200,00');
    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledWith({
      kind: 'fixed',
      groupId: works.id,
      description: 'Portaria',
      supplier: 'Segura Ltda',
      startMonth: '2026-09',
      endMonth: '2027-08',
      amount: 4200,
      installmentCount: null,
      currentInstallment: null,
    });
    expect(await result).toBe(true);
  });

  it('cadastra despesa fixa sem fornecedor e sem data final', async () => {
    const { save } = setup();

    typeIn(TAG, 'description', 'Portaria');
    typeIn(TAG, 'amount', '4200');
    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledWith(
      expect.objectContaining({ supplier: null, endMonth: null, amount: 4200 }),
    );
  });

  it('cadastra despesa unica sem mes final', async () => {
    const { save } = setup();

    chooseKind('single');
    typeIn(TAG, 'description', 'Conserto do portao');
    typeIn(TAG, 'amount', '350,50');
    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledWith({
      kind: 'single',
      groupId: ordinary.id,
      description: 'Conserto do portao',
      supplier: null,
      startMonth: '2026-11',
      endMonth: null,
      amount: 350.5,
      installmentCount: null,
      currentInstallment: null,
    });
  });

  it('cadastra despesa variavel sem valor', async () => {
    const { save } = setup();

    chooseKind('variable');
    typeIn(TAG, 'description', 'Conta de luz');
    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledWith({
      kind: 'variable',
      groupId: ordinary.id,
      description: 'Conta de luz',
      supplier: null,
      startMonth: '2026-11',
      endMonth: null,
      amount: null,
      installmentCount: null,
      currentInstallment: null,
    });
  });

  it('cadastra despesa parcelada com a parcela do mes inicial', async () => {
    const { save } = setup();

    chooseKind('installment');
    typeIn(TAG, 'description', 'Elevador');
    typeIn(TAG, 'amount', '1000');
    typeIn(TAG, 'installmentCount', '6');
    typeIn(TAG, 'currentInstallment', '2');
    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledWith({
      kind: 'installment',
      groupId: ordinary.id,
      description: 'Elevador',
      supplier: null,
      startMonth: '2026-11',
      endMonth: null,
      amount: 1000,
      installmentCount: 6,
      currentInstallment: 2,
    });
  });

  it('nao grava com campos obrigatorios vazios e mostra os erros', () => {
    const { save } = setup();

    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual([
      'Informe a descrição da despesa',
      'Informe um valor maior que zero, com até 2 casas decimais',
    ]);
  });

  it('limita descricao e fornecedor a 120 caracteres', () => {
    const { save } = setup();

    typeIn(TAG, 'description', 'a'.repeat(121));
    typeIn(TAG, 'supplier', 'b'.repeat(121));
    typeIn(TAG, 'amount', '10');
    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual([
      'A descrição pode ter até 120 caracteres',
      'O fornecedor pode ter até 120 caracteres',
    ]);
  });

  it('exige de 2 a 120 parcelas', () => {
    const { save } = setup();

    chooseKind('installment');
    typeIn(TAG, 'description', 'Elevador');
    typeIn(TAG, 'amount', '1000');
    typeIn(TAG, 'installmentCount', '1');
    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual(['Informe de 2 a 120 parcelas']);
  });

  it('exige que a parcela do mes inicial esteja entre 1 e o total', () => {
    const { save } = setup();

    chooseKind('installment');
    typeIn(TAG, 'description', 'Elevador');
    typeIn(TAG, 'amount', '1000');
    typeIn(TAG, 'installmentCount', '6');
    typeIn(TAG, 'currentInstallment', '7');
    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual(['Informe uma parcela entre 1 e o total de parcelas']);
  });

  it('revalida a parcela do mes quando o total muda', () => {
    setup();

    chooseKind('installment');
    typeIn(TAG, 'installmentCount', '6');
    typeIn(TAG, 'currentInstallment', '5');
    typeIn(TAG, 'installmentCount', '3');
    submitDialog(TAG);
    TestBed.tick();

    expect(errorsOf(TAG)).toContain('Informe uma parcela entre 1 e o total de parcelas');
  });

  it('nao aceita mes final antes do mes inicial', () => {
    const { save } = setup();

    typeIn(TAG, 'description', 'Portaria');
    typeIn(TAG, 'amount', '4200');
    chooseIn(TAG, 'startMonth', '2026-11');
    chooseIn(TAG, 'endMonth', '2026-10');
    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual(['O mês final não pode ser anterior ao mês inicial']);
  });

  it('aceita o mes final de novo quando o mes inicial passa a ser anterior', async () => {
    const { save } = setup();

    typeIn(TAG, 'description', 'Portaria');
    typeIn(TAG, 'amount', '4200');
    chooseIn(TAG, 'endMonth', '2026-10');
    chooseIn(TAG, 'startMonth', '2026-09');
    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledWith(expect.objectContaining({ endMonth: '2026-10' }));
  });

  it('continua aberto com o erro quando a gravacao e recusada', async () => {
    const { save } = setup();
    save.mockResolvedValue('Já existe uma despesa com esta descrição');

    typeIn(TAG, 'description', 'Portaria');
    typeIn(TAG, 'amount', '4200');
    submitDialog(TAG);
    await flushPromises();
    TestBed.tick();

    expect(alertOf(TAG)).toBe('Já existe uma despesa com esta descrição');
  });

  it('cancelar fecha sem gravar', async () => {
    const { save, result } = setup();

    dialogOf(TAG).querySelector<HTMLButtonElement>('[data-testid="cancel"]')!.click();

    expect(await result).toBe(false);
    expect(save).not.toHaveBeenCalled();
  });
});

describe('ExpenseDialog (editar despesa)', () => {
  afterEach(removeOverlays);

  const current = expense({
    groupId: ordinary.id,
    description: 'Portaria',
    supplier: 'Segura Ltda',
    kind: 'fixed',
    startMonth: '2026-09',
  });

  it('mostra so descricao, fornecedor e grupo, preenchidos', () => {
    setup(current);

    expect(dialogOf(TAG).querySelector('h2')?.textContent?.trim()).toBe('Editar despesa');
    expect(fieldOf(TAG, 'description').value).toBe('Portaria');
    expect(fieldOf(TAG, 'supplier').value).toBe('Segura Ltda');
    expect(fieldOf(TAG, 'groupId').value).toBe(ordinary.id);
    expect(['kind', 'startMonth', 'endMonth', 'amount'].map(has)).toEqual([
      false,
      false,
      false,
      false,
    ]);
    expect(submitButtonOf(TAG).textContent?.trim()).toBe('Salvar alterações');
  });

  it('so habilita o botao depois de mudar algo', () => {
    setup(current);
    expect(submitButtonOf(TAG).disabled).toBe(true);

    typeIn(TAG, 'description', 'Portaria 24h');
    TestBed.tick();
    expect(submitButtonOf(TAG).disabled).toBe(false);

    typeIn(TAG, 'description', 'Portaria');
    TestBed.tick();
    expect(submitButtonOf(TAG).disabled).toBe(true);
  });

  it('grava as mudancas mantendo os demais dados da despesa', async () => {
    const { save, result } = setup(current);

    typeIn(TAG, 'supplier', '');
    chooseIn(TAG, 'groupId', works.id);
    submitDialog(TAG);
    await flushPromises();

    expect(save).toHaveBeenCalledWith(
      expect.objectContaining({
        kind: 'fixed',
        groupId: works.id,
        description: 'Portaria',
        supplier: null,
        startMonth: '2026-09',
      }),
    );
    expect(await result).toBe(true);
  });

  it('exige a descricao', () => {
    const { save } = setup(current);

    typeIn(TAG, 'description', '   ');
    submitDialog(TAG);
    TestBed.tick();

    expect(save).not.toHaveBeenCalled();
    expect(errorsOf(TAG)).toEqual(['Informe a descrição da despesa']);
  });
});
