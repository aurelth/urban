import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ApportionmentChanged,
  ApportionmentMethod,
  BatchCreated,
  Unit,
  UnitPage,
  UnitPayload,
  UnitsQuery,
  UnitsSummary,
  UpdateUnitPayload,
} from './units.models';

const BASE = '/api/units';

/** Chamadas HTTP das unidades, uma por endpoint. */
@Injectable({ providedIn: 'root' })
export class UnitsApi {
  private readonly http = inject(HttpClient);

  list(query: UnitsQuery): Observable<UnitPage> {
    return this.http.get<UnitPage>(BASE, { params: toParams(query) });
  }

  summary(): Observable<UnitsSummary> {
    return this.http.get<UnitsSummary>(`${BASE}/summary`);
  }

  create(payload: UnitPayload): Observable<Unit> {
    return this.http.post<Unit>(BASE, payload);
  }

  update(id: string, payload: UpdateUnitPayload): Observable<Unit> {
    return this.http.put<Unit>(`${BASE}/${id}`, payload);
  }

  deactivate(id: string, version: string): Observable<Unit> {
    return this.http.post<Unit>(`${BASE}/${id}/deactivate`, { version });
  }

  reactivate(id: string, version: string): Observable<Unit> {
    return this.http.post<Unit>(`${BASE}/${id}/reactivate`, { version });
  }

  createBatch(units: readonly UnitPayload[]): Observable<BatchCreated> {
    return this.http.post<BatchCreated>(`${BASE}/batch`, { units });
  }

  changeMethod(version: string, method: ApportionmentMethod): Observable<ApportionmentChanged> {
    return this.http.put<ApportionmentChanged>(`${BASE}/apportionment-method`, { version, method });
  }
}

/** Só vão os filtros preenchidos; texto com espaços nas pontas é aparado. */
function toParams(query: UnitsQuery): HttpParams {
  let params = new HttpParams().set('status', query.status);
  if (query.type !== null) {
    params = params.set('type', query.type);
  }
  const block = query.block.trim();
  if (block !== '') {
    params = params.set('block', block);
  }
  const search = query.search.trim();
  if (search !== '') {
    params = params.set('search', search);
  }
  return params.set('page', query.page).set('pageSize', query.pageSize);
}
