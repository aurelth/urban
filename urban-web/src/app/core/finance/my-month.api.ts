import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { MyMonth } from './my-month.models';

const BASE = '/api/finance/me';

/** Chamada HTTP do painel financeiro do morador. */
@Injectable({ providedIn: 'root' })
export class MyMonthApi {
  private readonly http = inject(HttpClient);

  get(month: string): Observable<MyMonth> {
    return this.http.get<MyMonth>(`${BASE}/months/${month}`);
  }
}
