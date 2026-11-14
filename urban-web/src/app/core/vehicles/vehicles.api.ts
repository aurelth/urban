import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  UpdateVehiclePayload,
  Vehicle,
  VehiclePage,
  VehiclePayload,
  VehiclesQuery,
} from './vehicles.models';

const BASE = '/api/vehicles';

/** Chamadas HTTP dos veículos, uma por endpoint. */
@Injectable({ providedIn: 'root' })
export class VehiclesApi {
  private readonly http = inject(HttpClient);

  list(query: VehiclesQuery): Observable<VehiclePage> {
    return this.http.get<VehiclePage>(BASE, { params: toParams(query) });
  }

  get(id: string): Observable<Vehicle> {
    return this.http.get<Vehicle>(`${BASE}/${id}`);
  }

  create(payload: VehiclePayload): Observable<Vehicle> {
    return this.http.post<Vehicle>(BASE, payload);
  }

  update(id: string, payload: UpdateVehiclePayload): Observable<Vehicle> {
    return this.http.put<Vehicle>(`${BASE}/${id}`, payload);
  }

  /** A exclusão é definitiva: a linha some e a placa fica livre. */
  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${BASE}/${id}`);
  }
}

/** Só vão os filtros preenchidos; a busca com espaços nas pontas é aparada. */
function toParams(query: VehiclesQuery): HttpParams {
  let params = new HttpParams();
  if (query.unitId !== null) {
    params = params.set('unitId', query.unitId);
  }
  const search = query.search.trim();
  if (search !== '') {
    params = params.set('search', search);
  }
  return params.set('page', query.page).set('pageSize', query.pageSize);
}
