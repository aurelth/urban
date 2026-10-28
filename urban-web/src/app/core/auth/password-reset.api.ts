import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/** Chamadas HTTP da recuperação de senha: pedir o link por e-mail e trocar a senha com ele. */
@Injectable({ providedIn: 'root' })
export class PasswordResetApi {
  private readonly http = inject(HttpClient);

  requestLink(email: string): Observable<void> {
    return this.http.post<void>('/api/auth/forgot-password', { email });
  }

  reset(token: string, password: string): Observable<void> {
    return this.http.post<void>('/api/auth/reset-password', { token, password });
  }
}
