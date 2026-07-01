import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-base';
import {
  AddSetLogRequest,
  CompleteSessionResultDto,
  SetLogDto,
  WorkoutSessionDto,
} from '../models/training.models';

@Injectable({ providedIn: 'root' })
export class SessionService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  getById(id: string): Observable<WorkoutSessionDto> {
    return this.http.get<WorkoutSessionDto>(`${this.apiUrl}/sessions/${id}`);
  }

  start(id: string): Observable<WorkoutSessionDto> {
    return this.http.post<WorkoutSessionDto>(`${this.apiUrl}/sessions/${id}/start`, {});
  }

  complete(id: string): Observable<CompleteSessionResultDto> {
    return this.http.post<CompleteSessionResultDto>(`${this.apiUrl}/sessions/${id}/complete`, {});
  }

  addSet(planId: string, request: AddSetLogRequest): Observable<SetLogDto> {
    return this.http.post<SetLogDto>(`${this.apiUrl}/exercise-plans/${planId}/sets`, request);
  }

  // UpdateSetLogRequest has the same shape as AddSetLogRequest.
  updateSet(setId: string, request: AddSetLogRequest): Observable<SetLogDto> {
    return this.http.put<SetLogDto>(`${this.apiUrl}/sets/${setId}`, request);
  }

  deleteSet(setId: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/sets/${setId}`);
  }
}
