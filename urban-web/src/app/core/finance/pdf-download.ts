import { HttpClient } from '@angular/common/http';
import { Observable, map } from 'rxjs';

/** O PDF e o nome que a API sugeriu para ele. */
export interface FileDownload {
  readonly blob: Blob;
  readonly fileName: string;
}

/** Esqueleto: baixa o arquivo, mas ainda não lê o nome sugerido nem devolve o corpo da falha. */
export function downloadPdf(
  http: HttpClient,
  url: string,
  fallbackName: string,
): Observable<FileDownload> {
  return http
    .get(url, { responseType: 'blob', observe: 'response' })
    .pipe(map((response) => ({ blob: response.body!, fileName: fallbackName })));
}
