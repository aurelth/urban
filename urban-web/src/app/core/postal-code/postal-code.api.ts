import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, catchError, map, of } from 'rxjs';
import { PostalCodeAddress, PostalCodeLookup } from './postal-code.models';

const NOT_FOUND = 404;

/**
 * Consulta de CEP, que passa pela nossa API (o navegador nunca fala com BrasilAPI ou ViaCEP). Nenhuma falha
 * vira erro do Observable: a tela só precisa saber se achou, se o CEP não existe ou se a consulta não deu.
 * A sessão vencida (401) já foi tratada pelo interceptor, então aqui também vira "indisponível".
 */
@Injectable({ providedIn: 'root' })
export class PostalCodeApi {
  private readonly http = inject(HttpClient);

  /** @param postalCode Os oito dígitos, sem máscara. */
  lookup(postalCode: string): Observable<PostalCodeLookup> {
    return this.http.get<unknown>(`/api/postal-codes/${postalCode}`).pipe(
      map((body): PostalCodeLookup => {
        return isAddress(body) ? { status: 'found', address: body } : { status: 'unavailable' };
      }),
      catchError((error: unknown) =>
        of<PostalCodeLookup>(
          error instanceof HttpErrorResponse && error.status === NOT_FOUND
            ? { status: 'not-found' }
            : { status: 'unavailable' },
        ),
      ),
    );
  }
}

function isAddress(body: unknown): body is PostalCodeAddress {
  if (typeof body !== 'object' || body === null) {
    return false;
  }
  const fields = body as Record<string, unknown>;
  return ['postalCode', 'street', 'neighborhood', 'city', 'state'].every(
    (name) => typeof fields[name] === 'string',
  );
}
