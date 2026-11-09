import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ConsumptionTariff,
  ConsumptionTariffPayload,
  Expense,
  ExpenseEntry,
  ExpenseGroup,
  ExpensePayload,
  ExpenseStatusFilter,
  FinanceSettings,
  MonthEntries,
  MonthReadings,
  ReadingInput,
  SettableEntryStatus,
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

  setGroupReserveFundBase(id: string, version: string, enabled: boolean): Observable<ExpenseGroup> {
    return this.http.put<ExpenseGroup>(`${BASE}/expense-groups/${id}/reserve-fund-base`, {
      version,
      enabled,
    });
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

  closeMonth(month: string, dueDate: string): Observable<MonthEntries> {
    return this.http.post<MonthEntries>(`${BASE}/months/${month}/close`, { dueDate });
  }

  reopenMonth(month: string): Observable<MonthEntries> {
    return this.http.post<MonthEntries>(`${BASE}/months/${month}/reopen`, {});
  }

  setEntryAmount(id: string, version: string, amount: number): Observable<ExpenseEntry> {
    return this.http.put<ExpenseEntry>(`${BASE}/entries/${id}/amount`, { version, amount });
  }

  setEntryStatus(
    id: string,
    version: string,
    status: SettableEntryStatus,
  ): Observable<ExpenseEntry> {
    return this.http.put<ExpenseEntry>(`${BASE}/entries/${id}/status`, { version, status });
  }

  /** Da tarifa que começou mais recentemente para a mais antiga. */
  listTariffs(): Observable<ConsumptionTariff[]> {
    return this.http.get<ConsumptionTariff[]>(`${BASE}/consumption-tariffs`);
  }

  /** Guarda a tarifa que vale a partir do mês; repetir o mês a substitui. */
  saveTariff(month: string, payload: ConsumptionTariffPayload): Observable<ConsumptionTariff> {
    return this.http.put<ConsumptionTariff>(`${BASE}/consumption-tariffs/${month}`, payload);
  }

  deleteTariff(month: string): Observable<void> {
    return this.http.delete<void>(`${BASE}/consumption-tariffs/${month}`);
  }

  getSettings(): Observable<FinanceSettings> {
    return this.http.get<FinanceSettings>(`${BASE}/settings`);
  }

  setReserveFundPercent(reserveFundPercent: number): Observable<FinanceSettings> {
    return this.http.put<FinanceSettings>(`${BASE}/settings`, { reserveFundPercent });
  }

  getReadings(month: string): Observable<MonthReadings> {
    return this.http.get<MonthReadings>(`${BASE}/months/${month}/readings`);
  }

  saveReadings(month: string, readings: readonly ReadingInput[]): Observable<MonthReadings> {
    return this.http.put<MonthReadings>(`${BASE}/months/${month}/readings`, { readings });
  }
}
