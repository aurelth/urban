import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ManagersPage, ManagersQuery, PlatformSummary } from './admin.models';

const BASE = '/api/admin';

/** Chamadas HTTP do painel do administrador da plataforma: resumo e síndicos. */
@Injectable({ providedIn: 'root' })
export class AdminApi {
  private readonly http = inject(HttpClient);

  summary(): Observable<PlatformSummary> {
    return this.http.get<PlatformSummary>(`${BASE}/summary`);
  }

  listManagers(query: ManagersQuery): Observable<ManagersPage> {
    return this.http.get<ManagersPage>(`${BASE}/managers`, { params: toParams(query) });
  }

  suspend(id: string, reason: string): Observable<void> {
    return this.http.post<void>(`${BASE}/managers/${id}/suspend`, { reason });
  }

  reactivate(id: string): Observable<void> {
    return this.http.post<void>(`${BASE}/managers/${id}/reactivate`, null);
  }
}

/** Só vão os filtros preenchidos; a busca com espaços nas pontas é aparada. */
function toParams(query: ManagersQuery): HttpParams {
  let params = new HttpParams();
  if (query.status !== '') {
    params = params.set('status', query.status);
  }
  const search = query.search.trim();
  if (search !== '') {
    params = params.set('search', search);
  }
  return params.set('page', query.page).set('pageSize', query.pageSize);
}
