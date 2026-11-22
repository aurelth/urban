import { Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import {
  Announcement,
  AnnouncementPage,
  AnnouncementPayload,
  UpdateAnnouncementPayload,
} from './announcements.models';

const NOT_IMPLEMENTED = 'Ainda não implementado';

/** Chamadas HTTP dos comunicados do síndico, uma por endpoint. */
@Injectable({ providedIn: 'root' })
export class AnnouncementsApi {
  list(_page: number, _pageSize: number): Observable<AnnouncementPage> {
    throw new Error(NOT_IMPLEMENTED);
  }

  create(_payload: AnnouncementPayload): Observable<Announcement> {
    throw new Error(NOT_IMPLEMENTED);
  }

  update(_id: string, _payload: UpdateAnnouncementPayload): Observable<Announcement> {
    throw new Error(NOT_IMPLEMENTED);
  }

  remove(_id: string, _version: string): Observable<void> {
    throw new Error(NOT_IMPLEMENTED);
  }
}
