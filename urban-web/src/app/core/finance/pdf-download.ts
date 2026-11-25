import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Observable, catchError, from, map, mergeMap, throwError } from 'rxjs';
import { downloadFileName } from './download-file-name';

/** O PDF e o nome que a API sugeriu para ele. */
export interface FileDownload {
  readonly blob: Blob;
  readonly fileName: string;
}

/**
 * Baixa um PDF pelo cliente HTTP (e pelo interceptor do token, por isso não é um link simples). Quando a API não sugere um
 * nome, vale `fallbackName`. A falha chega com o corpo legível, para o código da API não se perder.
 */
export function downloadPdf(
  http: HttpClient,
  url: string,
  fallbackName: string,
): Observable<FileDownload> {
  return http.get(url, { responseType: 'blob', observe: 'response' }).pipe(
    map((response) => ({
      blob: response.body!,
      fileName: downloadFileName(response.headers.get('Content-Disposition'), fallbackName),
    })),
    catchError((error: unknown) =>
      from(withReadableBody(error)).pipe(mergeMap((readable) => throwError(() => readable))),
    ),
  );
}

/**
 * Com `responseType: 'blob'` o corpo da falha (application/problem+json) também chega como arquivo, e o código da API se
 * perderia. Lê o texto e refaz o erro com o JSON, para o resto do front tratar como qualquer outra falha.
 */
async function withReadableBody(error: unknown): Promise<unknown> {
  if (!(error instanceof HttpErrorResponse) || !(error.error instanceof Blob)) {
    return error;
  }
  return new HttpErrorResponse({
    error: await readProblem(error.error),
    headers: error.headers,
    status: error.status,
    statusText: error.statusText,
    url: error.url ?? undefined,
  });
}

async function readProblem(blob: Blob): Promise<unknown> {
  try {
    return JSON.parse(await readText(blob));
  } catch {
    return null;
  }
}

/** `Blob.text()` ainda não existe em todos os ambientes (o jsdom dos testes, por exemplo); o FileReader existe em todos. */
function readText(blob: Blob): Promise<string> {
  return new Promise((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = () => reject(reader.error);
    reader.readAsText(blob);
  });
}
