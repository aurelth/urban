import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { RegisterCondominiumPayload, RegisteredCondominium } from './condominium.models';

/** Chamadas HTTP do condomínio, uma por endpoint. */
@Injectable({ providedIn: 'root' })
export class CondominiumApi {
  private readonly http = inject(HttpClient);

  register(payload: RegisterCondominiumPayload): Observable<RegisteredCondominium> {
    return this.http.post<RegisteredCondominium>('/api/condominiums', payload);
  }
}
