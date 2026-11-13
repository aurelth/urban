import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { ContactReceipt } from './contact.models';

const BASE = '/api/contact/messages';

/** Chamada HTTP do contato do síndico com o dono da plataforma. */
@Injectable({ providedIn: 'root' })
export class ContactApi {
  private readonly http = inject(HttpClient);

  send(subject: string, body: string): Observable<ContactReceipt> {
    return this.http.post<ContactReceipt>(BASE, { subject: subject.trim(), body: body.trim() });
  }
}
