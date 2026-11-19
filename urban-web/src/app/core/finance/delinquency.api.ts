import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { Delinquency, DelinquencyQuery } from './delinquency.models';

const BASE = '/api/finance';

/** Chamada HTTP da inadimplência. */
@Injectable({ providedIn: 'root' })
export class DelinquencyApi {
  private readonly http = inject(HttpClient);

  list(query: DelinquencyQuery): Observable<Delinquency> {
    const params = new HttpParams().set('page', query.page).set('pageSize', query.pageSize);
    return this.http.get<Delinquency>(`${BASE}/delinquency`, { params });
  }
}
