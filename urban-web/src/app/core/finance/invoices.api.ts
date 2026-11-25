import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { FileDownload, downloadPdf } from './pdf-download';

const BASE = '/api/finance';
const FALLBACK_NAME = 'fatura.pdf';

/** Chamada HTTP da fatura em PDF. Passa pelo cliente HTTP (e pelo interceptor do token), por isso não é um link simples. */
@Injectable({ providedIn: 'root' })
export class InvoicesApi {
  private readonly http = inject(HttpClient);

  download(chargeId: string): Observable<FileDownload> {
    return downloadPdf(this.http, `${BASE}/charges/${chargeId}/invoice`, FALLBACK_NAME);
  }

  /** A fatura da própria unidade do morador: a API recusa (404) qualquer cobrança que não seja dele. */
  downloadMine(chargeId: string): Observable<FileDownload> {
    return downloadPdf(this.http, `${BASE}/me/charges/${chargeId}/invoice`, FALLBACK_NAME);
  }
}
