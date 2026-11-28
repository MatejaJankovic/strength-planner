import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-base';
import { PrivacyNoticeDto } from '../models/privacy.models';

@Injectable({ providedIn: 'root' })
export class PrivacyService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  notice(): Observable<PrivacyNoticeDto> {
    return this.http.get<PrivacyNoticeDto>(`${this.apiUrl}/privacy`);
  }
}
