import { provideHttpClient } from '@angular/common/http';
import {
  HttpTestingController,
  TestRequest,
  provideHttpClientTesting,
} from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ExpensePayload } from '../../core/finance/finance.models';
import { flushPromises } from '../../testing/async';
import { problem } from '../../testing/auth.fixtures';
import { expense, expenseGroup, installmentExpense } from '../../testing/finance.fixtures';
import { ExpensesStore } from './expenses.store';

const GROUP = expenseGroup();

const PAYLOAD: ExpensePayload = {
  kind: 'single',
  groupId: GROUP.id,
  description: 'Pintura',
  supplier: null,
  startMonth: '2026-11',
  endMonth: null,
  amount: 1500,
  installmentCount: null,
  currentInstallment: null,
};

function setup(): { store: InstanceType<typeof ExpensesStore>; http: HttpTestingController } {
  TestBed.configureTestingModule({
    providers: [provideHttpClient(), provideHttpClientTesting(), ExpensesStore],
  });
  return { store: TestBed.inject(ExpensesStore), http: TestBed.inject(HttpTestingController) };
}

function expensesRequest(http: HttpTestingController): TestRequest {
  return http.expectOne(
    (request) => request.url === '/api/finance/expenses' && request.method === 'GET',
  );
}

function fail(status: number, code: string): [unknown, { status: number; statusText: string }] {
  return [problem(status, code), { status, statusText: 'Erro' }];
}

/** Responde a carga completa, na ordem em que a tela a pede: o grupo padrão primeiro, depois grupos e despesas. */
async function loaded(
  store: InstanceType<typeof ExpensesStore>,
  http: HttpTestingController,
  expenses = [expense({ groupId: GROUP.id })],
): Promise<void> {
  const loading = store.load();
  http.expectOne('/api/finance/expense-groups/default').flush(GROUP);
  await flushPromises();
  http.expectOne({ method: 'GET', url: '/api/finance/expense-groups' }).flush([GROUP]);
  expensesRequest(http).flush(expenses);
  await loading;
}

function answerRefresh(http: HttpTestingController, expenses = [expense()]): void {
  http.expectOne({ method: 'GET', url: '/api/finance/expense-groups' }).flush([GROUP]);
  expensesRequest(http).flush(expenses);
}

describe('ExpensesStore', () => {
  afterEach(() => TestBed.inject(HttpTestingController).verify());

  it('comeca carregando, sem despesas, nas ativas', () => {
    const { store } = setup();

    expect(store.status()).toBe('loading');
    expect(store.filter()).toBe('active');
    expect(store.expenses()).toEqual([]);
    expect(store.groups()).toEqual([]);
    expect(store.busy()).toBe(false);
  });

  describe('load', () => {
    it('garante o grupo padrao e so depois busca grupos e despesas', async () => {
      const { store, http } = setup();

      const loading = store.load();
      http.expectOne('/api/finance/expense-groups/default').flush(GROUP);
      await flushPromises();
      http.expectOne({ method: 'GET', url: '/api/finance/expense-groups' }).flush([GROUP]);
      const request = expensesRequest(http);
      expect(request.request.params.get('status')).toBe('active');
      request.flush([expense()]);
      await loading;

      expect(store.status()).toBe('ready');
      expect(store.groups()).toEqual([GROUP]);
      expect(store.expenses()).toHaveLength(1);
    });

    it('vira "erro" quando alguma busca falha e pode tentar de novo', async () => {
      const { store, http } = setup();
      const first = store.load();
      http.expectOne('/api/finance/expense-groups/default').flush(null, {
        status: 500,
        statusText: 'Erro',
      });
      await first;
      expect(store.status()).toBe('error');

      await loaded(store, http);

      expect(store.status()).toBe('ready');
    });
  });

  it('guarda o nome de cada grupo para a tabela', async () => {
    const { store, http } = setup();
    await loaded(store, http);

    expect(store.groupName(GROUP.id)).toBe('Despesas ordinárias');
    expect(store.groupName('desconhecido')).toBe('—');
  });

  describe('filterBy', () => {
    it('busca de novo com a situacao escolhida', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const filtering = store.filterBy('ended');
      const request = expensesRequest(http);
      expect(request.request.params.get('status')).toBe('ended');
      request.flush([expense({ isEnded: true })]);
      await filtering;

      expect(store.filter()).toBe('ended');
      expect(store.expenses()[0].isEnded).toBe(true);
      expect(store.busy()).toBe(false);
    });

    it('vira "erro" quando a busca falha', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const filtering = store.filterBy('all');
      expensesRequest(http).flush(null, { status: 500, statusText: 'Erro' });
      await filtering;

      expect(store.status()).toBe('error');
    });
  });

  describe('gravacoes', () => {
    it('cadastra a despesa, atualiza a tela e devolve nulo', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const creating = store.createExpense(PAYLOAD);
      expect(store.busy()).toBe(true);
      const post = http.expectOne({ method: 'POST', url: '/api/finance/expenses' });
      expect(post.request.body).toEqual(PAYLOAD);
      post.flush(expense());
      await flushPromises();
      answerRefresh(http, [expense(), expense()]);

      expect(await creating).toBeNull();
      expect(store.expenses()).toHaveLength(2);
      expect(store.busy()).toBe(false);
    });

    it('edita com a versao lida', async () => {
      const { store, http } = setup();
      const target = expense({ version: '11' });
      await loaded(store, http, [target]);

      const editing = store.updateExpense(target, {
        description: 'Portaria 24h',
        supplier: 'Segura',
        groupId: GROUP.id,
      });
      const put = http.expectOne({ method: 'PUT', url: `/api/finance/expenses/${target.id}` });
      expect(put.request.body).toEqual({
        version: '11',
        description: 'Portaria 24h',
        supplier: 'Segura',
        groupId: GROUP.id,
      });
      put.flush(target);
      await flushPromises();
      answerRefresh(http, [target]);

      expect(await editing).toBeNull();
    });

    it('reajusta o valor, muda uma parcela e encerra, sempre com a versao lida', async () => {
      const { store, http } = setup();
      const fixed = expense({ version: '11' });
      const parcels = installmentExpense({ version: '12' });
      await loaded(store, http, [fixed, parcels]);

      const changing = store.changeAmount(fixed, '2026-12', 4400);
      const amount = http.expectOne({
        method: 'POST',
        url: `/api/finance/expenses/${fixed.id}/amount`,
      });
      expect(amount.request.body).toEqual({ version: '11', fromMonth: '2026-12', amount: 4400 });
      amount.flush(fixed);
      await flushPromises();
      answerRefresh(http, [fixed, parcels]);
      expect(await changing).toBeNull();

      const setting = store.setInstallment(parcels, 2, 120);
      const parcel = http.expectOne({
        method: 'PUT',
        url: `/api/finance/expenses/${parcels.id}/installments/2`,
      });
      expect(parcel.request.body).toEqual({ version: '12', amount: 120 });
      parcel.flush(parcels);
      await flushPromises();
      answerRefresh(http, [fixed, parcels]);
      expect(await setting).toBeNull();

      const ending = store.end(fixed, '2027-01');
      const end = http.expectOne({ method: 'POST', url: `/api/finance/expenses/${fixed.id}/end` });
      expect(end.request.body).toEqual({ version: '11', lastMonth: '2027-01' });
      end.flush(fixed);
      await flushPromises();
      answerRefresh(http, [fixed, parcels]);
      expect(await ending).toBeNull();
    });

    it('cria e renomeia grupos', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const creating = store.createGroup('Obras');
      const post = http.expectOne({ method: 'POST', url: '/api/finance/expense-groups' });
      expect(post.request.body).toEqual({ name: 'Obras' });
      post.flush(expenseGroup({ name: 'Obras' }));
      await flushPromises();
      answerRefresh(http);
      expect(await creating).toBeNull();

      const renaming = store.renameGroup(GROUP, 'Contratos');
      const put = http.expectOne({ method: 'PUT', url: `/api/finance/expense-groups/${GROUP.id}` });
      expect(put.request.body).toEqual({ version: '5', name: 'Contratos' });
      put.flush(GROUP);
      await flushPromises();
      answerRefresh(http);
      expect(await renaming).toBeNull();
    });

    it('devolve o problema sem atualizar quando a API recusa um dado', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const creating = store.createExpense({ ...PAYLOAD, amount: 0 });
      http
        .expectOne({ method: 'POST', url: '/api/finance/expenses' })
        .flush(...fail(400, 'expense.amount.invalid'));
      const result = await creating;

      expect(result?.code).toBe('expense.amount.invalid');
      expect(store.busy()).toBe(false);
    });

    it.each([
      [409, 'concurrency.conflict'],
      [404, 'expense.not_found'],
    ])('busca a tela de novo quando esta desatualizada (%i %s)', async (status, code) => {
      const { store, http } = setup();
      const target = expense();
      await loaded(store, http, [target]);

      const ending = store.end(target, '2027-01');
      http
        .expectOne({ method: 'POST', url: `/api/finance/expenses/${target.id}/end` })
        .flush(...fail(status, code));
      await flushPromises();
      answerRefresh(http, []);
      const result = await ending;

      expect(result?.code).toBe(code);
      expect(store.expenses()).toEqual([]);
    });

    it('vira "erro" se a atualizacao depois da gravacao tambem falhar', async () => {
      const { store, http } = setup();
      await loaded(store, http);

      const creating = store.createExpense(PAYLOAD);
      http.expectOne({ method: 'POST', url: '/api/finance/expenses' }).flush(expense());
      await flushPromises();
      http
        .expectOne({ method: 'GET', url: '/api/finance/expense-groups' })
        .flush(null, { status: 500, statusText: 'Erro' });
      expensesRequest(http).flush([]);
      await creating;

      expect(store.status()).toBe('error');
    });
  });
});
