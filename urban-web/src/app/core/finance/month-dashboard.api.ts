import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { MonthDashboard } from './month-dashboard.models';

const BASE = '/api/finance';

/** Chamada HTTP do painel financeiro do mês. */
@Injectable({ providedIn: 'root' })
export class MonthDashboardApi {
  private readonly http = inject(HttpClient);

  get(month: string): Observable<MonthDashboard> {
    return this.http.get<MonthDashboard>(`${BASE}/months/${month}/dashboard`);
  }
}
