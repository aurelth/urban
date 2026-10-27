import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { AccessTokenResponse, CurrentUser } from './auth.models';

/** Chamadas HTTP da autenticação, uma por endpoint. O estado fica no SessionStore. */
@Injectable({ providedIn: 'root' })
export class AuthApi {
  private readonly http = inject(HttpClient);

  login(email: string, password: string): Observable<AccessTokenResponse> {
    return this.http.post<AccessTokenResponse>('/api/auth/login', { email, password });
  }

  refresh(): Observable<AccessTokenResponse> {
    return this.http.post<AccessTokenResponse>('/api/auth/refresh', null);
  }

  logout(): Observable<void> {
    return this.http.post<void>('/api/auth/logout', null);
  }

  me(): Observable<CurrentUser> {
    return this.http.get<CurrentUser>('/api/auth/me');
  }
}
