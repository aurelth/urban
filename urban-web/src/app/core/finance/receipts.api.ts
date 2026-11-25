import { Injectable } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { FileDownload } from './pdf-download';

/** Esqueleto: ainda não chama a API. */
@Injectable({ providedIn: 'root' })
export class ReceiptsApi {
  download(paymentId: string): Observable<FileDownload> {
    return throwError(() => new Error(`Ainda não implementado: ${paymentId}`));
  }

  downloadMine(paymentId: string): Observable<FileDownload> {
    return throwError(() => new Error(`Ainda não implementado: ${paymentId}`));
  }
}
