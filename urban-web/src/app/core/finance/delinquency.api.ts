import { Observable } from 'rxjs';
import { Injectable } from '@angular/core';
import { Delinquency, DelinquencyQuery } from './delinquency.models';

/** Chamada HTTP da inadimplência. */
@Injectable({ providedIn: 'root' })
export class DelinquencyApi {
  list(query: DelinquencyQuery): Observable<Delinquency> {
    throw new Error(`Não implementado: ${JSON.stringify(query)}`);
  }
}
