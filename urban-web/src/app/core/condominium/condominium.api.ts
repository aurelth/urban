import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  MyCondominium,
  RegisterCondominiumPayload,
  RegisteredCondominium,
  UpdateManagerIdentityPayload,
  UpdateMyCondominiumPayload,
} from './condominium.models';

/** Chamadas HTTP do condomínio, uma por endpoint. */
@Injectable({ providedIn: 'root' })
export class CondominiumApi {
  private readonly http = inject(HttpClient);

  register(payload: RegisterCondominiumPayload): Observable<RegisteredCondominium> {
    return this.http.post<RegisteredCondominium>('/api/condominiums', payload);
  }

  mine(): Observable<MyCondominium> {
    return this.http.get<MyCondominium>('/api/condominiums/mine');
  }

  updateMine(payload: UpdateMyCondominiumPayload): Observable<MyCondominium> {
    return this.http.put<MyCondominium>('/api/condominiums/mine', payload);
  }

  updateManagerIdentity(payload: UpdateManagerIdentityPayload): Observable<MyCondominium> {
    return this.http.put<MyCondominium>('/api/condominiums/mine/manager-identity', payload);
  }
}
