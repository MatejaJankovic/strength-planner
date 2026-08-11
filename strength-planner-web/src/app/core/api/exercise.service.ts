import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Observable, of, shareReplay, tap } from 'rxjs';
import { API_BASE_URL } from './api-base';
import {
  CreateExerciseRequest,
  ExerciseDto,
  UpdateWeightStepRequest,
} from '../models/training.models';

@Injectable({ providedIn: 'root' })
export class ExerciseService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  private readonly exercisesSignal = signal<ExerciseDto[]>([]);
  readonly exercises = this.exercisesSignal.asReadonly();

  private request?: Observable<ExerciseDto[]>;

  /**
   * Loads the exercise catalog once and caches it in a signal. Concurrent
   * callers share the in-flight request; later callers get the cached list.
   */
  load(): Observable<ExerciseDto[]> {
    if (this.exercisesSignal().length > 0) {
      return of(this.exercisesSignal());
    }

    this.request ??= this.http.get<ExerciseDto[]>(`${this.apiUrl}/exercises`).pipe(
      tap((exercises) => this.exercisesSignal.set(exercises)),
      shareReplay(1),
    );

    return this.request;
  }

  /** Prazni keš — poziva se pri prijavi/odjavi da podaci ne procure između naloga. */
  reset(): void {
    this.exercisesSignal.set([]);
    this.request = undefined;
  }

  muscleGroups(): Observable<string[]> {
    return this.http.get<string[]>(`${this.apiUrl}/exercises/muscle-groups`);
  }

  /**
   * Sets the per-exercise load increment. Passing null clears the override and
   * restores the equipment default. The cached catalog is updated in place so
   * every screen sees the new step without a reload.
   */
  updateWeightStep(exerciseId: string, weightStepKg: number | null): Observable<ExerciseDto> {
    const request: UpdateWeightStepRequest = { weightStepKg };

    return this.http
      .put<ExerciseDto>(`${this.apiUrl}/exercises/${exerciseId}/weight-step`, request)
      .pipe(
        tap((updated) =>
          this.exercisesSignal.update((exercises) =>
            exercises.map((exercise) => (exercise.id === updated.id ? updated : exercise)),
          ),
        ),
      );
  }

  createCustom(request: CreateExerciseRequest): Observable<ExerciseDto> {
    return this.http.post<ExerciseDto>(`${this.apiUrl}/exercises`, request).pipe(
      tap((exercise) =>
        this.exercisesSignal.update((exercises) =>
          [...exercises, exercise].sort((a, b) => a.name.localeCompare(b.name)),
        ),
      ),
    );
  }
}
