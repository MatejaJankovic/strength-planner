import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-base';
import { E1rmPointDto, PersonalRecordDto, VolumeItemDto, WeeklyTonnageDto } from '../models/analytics.models';

@Injectable({ providedIn: 'root' })
export class AnalyticsService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  e1rmTrend(exerciseId: string): Observable<E1rmPointDto[]> {
    return this.http.get<E1rmPointDto[]>(`${this.apiUrl}/analytics/e1rm/${exerciseId}`);
  }

  volume(mesocycleId: string, week: number): Observable<VolumeItemDto[]> {
    const params = new HttpParams().set('mesocycleId', mesocycleId).set('week', week);
    return this.http.get<VolumeItemDto[]>(`${this.apiUrl}/analytics/volume`, { params });
  }

  /** Vraca naucene MEV/MRV granice na podrazumevane vrednosti. */
  resetVolumeLandmarks(): Observable<void> {
    return this.http.post<void>(`${this.apiUrl}/analytics/volume/landmarks/reset`, {});
  }

  personalRecords(): Observable<PersonalRecordDto[]> {
    return this.http.get<PersonalRecordDto[]>(`${this.apiUrl}/analytics/prs`);
  }

  tonnage(mesocycleId: string): Observable<WeeklyTonnageDto[]> {
    const params = new HttpParams().set('mesocycleId', mesocycleId);
    return this.http.get<WeeklyTonnageDto[]>(`${this.apiUrl}/analytics/tonnage`, { params });
  }
}
