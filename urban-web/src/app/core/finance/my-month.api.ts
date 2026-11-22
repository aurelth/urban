import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable, throwError } from 'rxjs';
import { MyMonth } from './my-month.models';

/** Chamada HTTP do painel financeiro do morador. */
@Injectable({ providedIn: 'root' })
export class MyMonthApi {
  // Esqueleto da fatia de testes: o corpo vem com a implementação.
  protected readonly http = inject(HttpClient);

  get(month: string): Observable<MyMonth> {
    return throwError(() => new Error(`sem implementação: ${month}`));
  }
}
