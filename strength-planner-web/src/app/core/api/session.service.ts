import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-base';
import {
  AddSetLogRequest,
  CompleteSessionResultDto,
  ExerciseAlternativeDto,
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

  skip(id: string): Observable<WorkoutSessionDto> {
    return this.http.post<WorkoutSessionDto>(`${this.apiUrl}/sessions/${id}/skip`, {});
  }

  unskip(id: string): Observable<WorkoutSessionDto> {
    return this.http.post<WorkoutSessionDto>(`${this.apiUrl}/sessions/${id}/unskip`, {});
  }

  alternatives(planId: string): Observable<ExerciseAlternativeDto[]> {
    return this.http.get<ExerciseAlternativeDto[]>(`${this.apiUrl}/sessions/plans/${planId}/alternatives`);
  }

  /** Menja vežbu do kraja bloka i vraća trening iz koga je zamena tražena. */
  substitute(planId: string, exerciseId: string): Observable<WorkoutSessionDto> {
    return this.http.post<WorkoutSessionDto>(`${this.apiUrl}/sessions/plans/${planId}/substitute`, {
      exerciseId,
    });
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
