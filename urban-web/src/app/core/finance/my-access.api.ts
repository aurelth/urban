import { HttpClient } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import { MyAccess } from './my-month.models';

/** Chamada HTTP de como a conta mora no condomínio. */
@Injectable({ providedIn: 'root' })
export class MyAccessApi {
  private readonly http = inject(HttpClient);

  get(): Observable<MyAccess> {
    return this.http.get<MyAccess>('/api/finance/me/access');
  }
}
