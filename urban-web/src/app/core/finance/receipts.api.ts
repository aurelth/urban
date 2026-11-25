import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, of } from 'rxjs';
import { FileDownload, downloadPdf } from './pdf-download';
import { ReceiptEmailResult } from './receipts.models';

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

  /** Pede o envio do recibo, em PDF, ao responsável pela unidade. A API responde 202: o e-mail sai em segundo plano. */
  sendByEmail(paymentId: string): Observable<ReceiptEmailResult> {
    return of({ recipientName: paymentId.slice(0, 0) });
  }
}
