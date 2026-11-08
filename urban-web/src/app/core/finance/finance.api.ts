import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Expense,
  ExpenseEntry,
  ExpenseGroup,
  ExpensePayload,
  ExpenseStatusFilter,
  MonthEntries,
  UpdateExpensePayload,
} from './finance.models';

const BASE = '/api/finance';

/** Chamadas HTTP do financeiro, uma por endpoint. */
@Injectable({ providedIn: 'root' })
export class FinanceApi {
  private readonly http = inject(HttpClient);

  listGroups(): Observable<ExpenseGroup[]> {
    return this.http.get<ExpenseGroup[]>(`${BASE}/expense-groups`);
  }

  createGroup(name: string): Observable<ExpenseGroup> {
    return this.http.post<ExpenseGroup>(`${BASE}/expense-groups`, { name });
  }

  ensureDefaultGroup(): Observable<ExpenseGroup> {
    return this.http.post<ExpenseGroup>(`${BASE}/expense-groups/default`, {});
  }

  renameGroup(id: string, version: string, name: string): Observable<ExpenseGroup> {
    return this.http.put<ExpenseGroup>(`${BASE}/expense-groups/${id}`, { version, name });
  }

  listExpenses(status: ExpenseStatusFilter): Observable<Expense[]> {
    return this.http.get<Expense[]>(`${BASE}/expenses`, {
      params: new HttpParams().set('status', status),
    });
  }

  createExpense(payload: ExpensePayload): Observable<Expense> {
    return this.http.post<Expense>(`${BASE}/expenses`, payload);
  }

  updateExpense(id: string, payload: UpdateExpensePayload): Observable<Expense> {
    return this.http.put<Expense>(`${BASE}/expenses/${id}`, payload);
  }

  changeAmount(
    id: string,
    version: string,
    fromMonth: string,
    amount: number,
  ): Observable<Expense> {
    return this.http.post<Expense>(`${BASE}/expenses/${id}/amount`, { version, fromMonth, amount });
  }

  setInstallment(id: string, version: string, number: number, amount: number): Observable<Expense> {
    return this.http.put<Expense>(`${BASE}/expenses/${id}/installments/${number}`, {
      version,
      amount,
    });
  }

  endExpense(id: string, version: string, lastMonth: string): Observable<Expense> {
    return this.http.post<Expense>(`${BASE}/expenses/${id}/end`, { version, lastMonth });
  }

  getMonth(month: string): Observable<MonthEntries> {
    return this.http.get<MonthEntries>(`${BASE}/months/${month}`);
  }

  openMonth(month: string): Observable<MonthEntries> {
    return this.http.post<MonthEntries>(`${BASE}/months/${month}/open`, {});
  }

  setEntryAmount(id: string, version: string, amount: number): Observable<ExpenseEntry> {
    return this.http.put<ExpenseEntry>(`${BASE}/entries/${id}/amount`, { version, amount });
  }
}
