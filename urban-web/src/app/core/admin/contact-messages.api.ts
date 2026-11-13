import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ContactMessagesPage, ContactMessagesQuery } from './contact-messages.models';

const BASE = '/api/admin/contact-messages';

/** Chamadas HTTP da caixa de mensagens do administrador da plataforma. */
@Injectable({ providedIn: 'root' })
export class ContactMessagesApi {
  private readonly http = inject(HttpClient);

  list(query: ContactMessagesQuery): Observable<ContactMessagesPage> {
    return this.http.get<ContactMessagesPage>(BASE, { params: toParams(query) });
  }

  markRead(id: string): Observable<void> {
    return this.http.post<void>(`${BASE}/${id}/read`, null);
  }
}

/** Só vai o filtro preenchido. */
function toParams(query: ContactMessagesQuery): HttpParams {
  let params = new HttpParams();
  if (query.status !== '') {
    params = params.set('status', query.status);
  }
  return params.set('page', query.page).set('pageSize', query.pageSize);
}
