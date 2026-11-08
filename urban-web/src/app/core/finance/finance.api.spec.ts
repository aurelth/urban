import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { FinanceApi } from './finance.api';
import { ExpensePayload } from './finance.models';

const PAYLOAD: ExpensePayload = {
  kind: 'installment',
  groupId: 'g1',
  description: 'Elevador',
  supplier: null,
  startMonth: '2026-11',
  endMonth: null,
  amount: 1000,
  installmentCount: 6,
  currentInstallment: 4,
};

describe('FinanceApi', () => {
  let api: FinanceApi;
  let http: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [provideHttpClient(), provideHttpClientTesting()],
    });
    api = TestBed.inject(FinanceApi);
    http = TestBed.inject(HttpTestingController);
  });

  afterEach(() => http.verify());

  function expectCall(method: string, url: string, body?: unknown): void {
    const request = http.expectOne(url);
    expect(request.request.method).toBe(method);
    if (body !== undefined) {
      expect(request.request.body).toEqual(body);
    }
    request.flush({});
  }

  it('lists the groups', () => {
    api.listGroups().subscribe();
    expectCall('GET', '/api/finance/expense-groups');
  });

  it('creates a group', () => {
    api.createGroup('Obras').subscribe();
    expectCall('POST', '/api/finance/expense-groups', { name: 'Obras' });
  });

  it('ensures the default group', () => {
    api.ensureDefaultGroup().subscribe();
    expectCall('POST', '/api/finance/expense-groups/default', {});
  });

  it('renames a group with the version that was read', () => {
    api.renameGroup('g1', '7', 'Reformas').subscribe();
    expectCall('PUT', '/api/finance/expense-groups/g1', { version: '7', name: 'Reformas' });
  });

  it('lists the expenses filtered by status', () => {
    api.listExpenses('ended').subscribe();
    const request = http.expectOne((r) => r.url === '/api/finance/expenses');
    expect(request.request.method).toBe('GET');
    expect(request.request.params.get('status')).toBe('ended');
    request.flush([]);
  });

  it('creates an expense', () => {
    api.createExpense(PAYLOAD).subscribe();
    expectCall('POST', '/api/finance/expenses', PAYLOAD);
  });

  it('edits an expense', () => {
    api
      .updateExpense('e1', {
        version: '3',
        description: 'Portaria',
        supplier: 'Segura',
        groupId: 'g2',
      })
      .subscribe();
    expectCall('PUT', '/api/finance/expenses/e1', {
      version: '3',
      description: 'Portaria',
      supplier: 'Segura',
      groupId: 'g2',
    });
  });

  it('changes the amount of a fixed expense from a month', () => {
    api.changeAmount('e1', '3', '2026-12', 4400).subscribe();
    expectCall('POST', '/api/finance/expenses/e1/amount', {
      version: '3',
      fromMonth: '2026-12',
      amount: 4400,
    });
  });

  it('changes one parcel', () => {
    api.setInstallment('e1', '3', 2, 120).subscribe();
    expectCall('PUT', '/api/finance/expenses/e1/installments/2', { version: '3', amount: 120 });
  });

  it('ends an expense', () => {
    api.endExpense('e1', '3', '2026-12').subscribe();
    expectCall('POST', '/api/finance/expenses/e1/end', { version: '3', lastMonth: '2026-12' });
  });

  it('reads a month', () => {
    api.getMonth('2026-11').subscribe();
    expectCall('GET', '/api/finance/months/2026-11');
  });

  it('opens a month', () => {
    api.openMonth('2026-11').subscribe();
    expectCall('POST', '/api/finance/months/2026-11/open', {});
  });

  it('sets the amount of an entry', () => {
    api.setEntryAmount('n1', '9', 812.34).subscribe();
    expectCall('PUT', '/api/finance/entries/n1/amount', { version: '9', amount: 812.34 });
  });
});
