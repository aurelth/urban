import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ResidentPage, ResidentsQuery } from './residents.models';

const BASE = '/api/residents';

/** Chamadas HTTP dos moradores, uma por endpoint. */
@Injectable({ providedIn: 'root' })
export class ResidentsApi {
  private readonly http = inject(HttpClient);

  list(query: ResidentsQuery): Observable<ResidentPage> {
    return this.http.get<ResidentPage>(BASE, { params: toParams(query) });
  }
}

/** Só vai a busca preenchida; texto com espaços nas pontas é aparado. */
function toParams(query: ResidentsQuery): HttpParams {
  let params = new HttpParams().set('status', query.status);
  const search = query.search.trim();
  if (search !== '') {
    params = params.set('search', search);
  }
  return params.set('page', query.page).set('pageSize', query.pageSize);
}
