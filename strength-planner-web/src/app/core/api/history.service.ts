import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-base';
import { TrainingHistoryDto } from '../models/training.models';

/**
 * Istorija treninga. Ne čuva ništa između poziva: spisak živi u ekranu koji ga prikazuje, pa
 * nema šta da ostane iza odjave (vidi `SessionDataLeak` iz treće runde).
 */
@Injectable({ providedIn: 'root' })
export class HistoryService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  page(page: number, pageSize: number): Observable<TrainingHistoryDto> {
    const params = new HttpParams().set('page', page).set('pageSize', pageSize);
    return this.http.get<TrainingHistoryDto>(`${this.apiUrl}/history`, { params });
  }
}
