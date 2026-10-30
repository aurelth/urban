import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';

/** Chamadas HTTP de quem já está logado sobre a própria conta: trocar a senha e trocar o e-mail. */
@Injectable({ providedIn: 'root' })
export class AccountApi {
  private readonly http = inject(HttpClient);

  changePassword(currentPassword: string, newPassword: string): Observable<void> {
    return this.http.post<void>('/api/auth/change-password', { currentPassword, newPassword });
  }

  requestEmailChange(password: string, newEmail: string): Observable<void> {
    return this.http.post<void>('/api/auth/change-email', { password, newEmail });
  }

  /** Rota pública: quem abre o link prova que lê a caixa do endereço novo, sem precisar de sessão. */
  confirmEmailChange(token: string): Observable<void> {
    return this.http.post<void>('/api/auth/confirm-email-change', { token });
  }
}
