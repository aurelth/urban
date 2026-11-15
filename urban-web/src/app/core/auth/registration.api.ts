import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { CurrentTerms } from './auth.models';

/** Chamadas HTTP do cadastro do síndico: pedir o cadastro, confirmar o e-mail e ler a versão dos termos. */
@Injectable({ providedIn: 'root' })
export class RegistrationApi {
  private readonly http = inject(HttpClient);

  register(email: string, name: string, condominiumName: string): Observable<void> {
    return this.http.post<void>('/api/auth/register', { email, name, condominiumName });
  }

  confirmEmail(token: string, password: string, termsVersion: string): Observable<void> {
    return this.http.post<void>('/api/auth/confirm-email', { token, password, termsVersion });
  }

  /** Confere o link sem gastá-lo: 204 quando serve, 400 com o motivo quando não serve mais. */
  checkEmailLink(token: string): Observable<void> {
    return this.http.post<void>('/api/auth/confirm-email/check', { token });
  }

  currentTerms(): Observable<CurrentTerms> {
    return this.http.get<CurrentTerms>('/api/terms/current');
  }
}
