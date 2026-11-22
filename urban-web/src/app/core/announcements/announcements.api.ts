import { HttpClient, HttpParams } from '@angular/common/http';
import { Injectable, inject } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Announcement,
  AnnouncementPage,
  AnnouncementPayload,
  UpdateAnnouncementPayload,
} from './announcements.models';

const BASE = '/api/announcements';

/** Chamadas HTTP dos comunicados do síndico, uma por endpoint. */
@Injectable({ providedIn: 'root' })
export class AnnouncementsApi {
  private readonly http = inject(HttpClient);

  list(page: number, pageSize: number): Observable<AnnouncementPage> {
    return this.http.get<AnnouncementPage>(BASE, {
      params: new HttpParams().set('page', page).set('pageSize', pageSize),
    });
  }

  create(payload: AnnouncementPayload): Observable<Announcement> {
    return this.http.post<Announcement>(BASE, payload);
  }

  update(id: string, payload: UpdateAnnouncementPayload): Observable<Announcement> {
    return this.http.put<Announcement>(`${BASE}/${id}`, payload);
  }

  /** A versão vai na consulta: a API não lê corpo no DELETE. */
  remove(id: string, version: string): Observable<void> {
    return this.http.delete<void>(`${BASE}/${id}`, { params: { version } });
  }
}
