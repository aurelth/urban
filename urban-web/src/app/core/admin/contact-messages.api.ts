import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  ContactMessagesPage,
  ContactMessagesQuery,
  ContactReplyReceipt,
} from './contact-messages.models';

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

  /**
   * Responde por e-mail ao síndico. Vai como multipart: o navegador define o Content-Type com o limite das partes, então
   * o cabeçalho nunca é escrito aqui.
   */
  reply(id: string, body: string, files: readonly File[]): Observable<ContactReplyReceipt> {
    const form = new FormData();
    form.append('body', body.trim());
    for (const file of files) {
      form.append('attachments', file, file.name);
    }
    return this.http.post<ContactReplyReceipt>(`${BASE}/${id}/replies`, form);
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
