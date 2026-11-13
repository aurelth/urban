import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RegistrationRequest } from './registration-requests.models';

const BASE = '/api/admin/registration-requests';

/** Chamadas HTTP dos pedidos de cadastro do síndico, só para o administrador. */
@Injectable({ providedIn: 'root' })
export class RegistrationRequestsApi {
  private readonly http = inject(HttpClient);

  list(): Observable<RegistrationRequest[]> {
    return this.http.get<RegistrationRequest[]>(BASE);
  }

  approve(id: string): Observable<void> {
    return this.http.post<void>(`${BASE}/${id}/approve`, null);
  }

  reject(id: string): Observable<void> {
    return this.http.post<void>(`${BASE}/${id}/reject`, null);
  }
}
