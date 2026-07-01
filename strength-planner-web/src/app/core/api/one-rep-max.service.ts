import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from './api-base';
import { CreateOneRepMaxRequest, OneRepMaxDto } from '../models/analytics.models';

@Injectable({ providedIn: 'root' })
export class OneRepMaxService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  private readonly oneRepMaxesSignal = signal<OneRepMaxDto[]>([]);
  readonly oneRepMaxes = this.oneRepMaxesSignal.asReadonly();

  load(): Observable<OneRepMaxDto[]> {
    return this.http
      .get<OneRepMaxDto[]>(`${this.apiUrl}/onerepmax`)
      .pipe(tap((list) => this.oneRepMaxesSignal.set(list)));
  }

  save(request: CreateOneRepMaxRequest): Observable<OneRepMaxDto> {
    return this.http
      .post<OneRepMaxDto>(`${this.apiUrl}/onerepmax`, request)
      .pipe(tap((saved) => this.upsertLocal(saved)));
  }

  // History of manual + estimated 1RM records for one exercise.
  // Backend route param is the exerciseId (not a 1RM record id).
  history(exerciseId: string): Observable<OneRepMaxDto[]> {
    return this.http.get<OneRepMaxDto[]>(`${this.apiUrl}/onerepmax/${exerciseId}/history`);
  }

  private upsertLocal(saved: OneRepMaxDto): void {
    this.oneRepMaxesSignal.update((list) => {
      const index = list.findIndex((item) => item.exerciseId === saved.exerciseId);
      if (index === -1) {
        return [...list, saved];
      }
      const next = [...list];
      next[index] = saved;
      return next;
    });
  }
}
