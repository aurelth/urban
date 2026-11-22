import { Injectable } from '@angular/core';
import { EMPTY, Observable } from 'rxjs';
import { MyAccess } from './my-month.models';

/** Chamada HTTP de como a conta mora no condomínio. */
@Injectable({ providedIn: 'root' })
export class MyAccessApi {
  get(): Observable<MyAccess> {
    return EMPTY;
  }
}
