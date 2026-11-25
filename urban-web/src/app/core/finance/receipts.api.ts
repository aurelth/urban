import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { FileDownload, downloadPdf } from './pdf-download';

const BASE = '/api/finance';
const FALLBACK_NAME = 'recibo.pdf';

/** Chamada HTTP do recibo em PDF, um por pagamento. */
@Injectable({ providedIn: 'root' })
export class ReceiptsApi {
  private readonly http = inject(HttpClient);

  download(paymentId: string): Observable<FileDownload> {
    return downloadPdf(this.http, `${BASE}/payments/${paymentId}/receipt`, FALLBACK_NAME);
  }

  /** O recibo de um pagamento da própria unidade do morador: a API recusa (404) o de qualquer outra. */
  downloadMine(paymentId: string): Observable<FileDownload> {
    return downloadPdf(this.http, `${BASE}/me/payments/${paymentId}/receipt`, FALLBACK_NAME);
  }
}
