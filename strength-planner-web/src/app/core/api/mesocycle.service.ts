import { HttpClient } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from './api-base';
import {
  GenerateMesocycleRequest,
  MesocycleDto,
  MesocycleSummaryDto,
  WorkoutTemplateDto,
} from '../models/training.models';

@Injectable({ providedIn: 'root' })
export class MesocycleService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  private readonly activeSignal = signal<MesocycleDto | null>(null);
  readonly active = this.activeSignal.asReadonly();

  /** Prazni korisnički keš (aktivni plan). */
  reset(): void {
    this.activeSignal.set(null);
  }

  /**
   * Ugrađeni šabloni, viđeni očima trenutnog korisnika: dani su skraćeni na njegov nivo
   * iskustva, a jedan je označen kao predlog za njegov broj trenažnih dana. Zato se i ne
   * keširaju — izmena profila menja odgovor.
   */
  templates(): Observable<WorkoutTemplateDto[]> {
    return this.http.get<WorkoutTemplateDto[]>(`${this.apiUrl}/templates`);
  }

  /** All mesocycles for the current user (summaries, newest-first per backend). */
  list(): Observable<MesocycleSummaryDto[]> {
    return this.http.get<MesocycleSummaryDto[]>(`${this.apiUrl}/mesocycles`);
  }

  create(request: GenerateMesocycleRequest): Observable<MesocycleDto> {
    return this.http
      .post<MesocycleDto>(`${this.apiUrl}/mesocycles`, request)
      .pipe(tap((mesocycle) => this.activeSignal.set(mesocycle)));
  }

  /** Active mesocycle with full week/session structure. 404 when none exists. */
  loadActive(): Observable<MesocycleDto> {
    return this.http
      .get<MesocycleDto>(`${this.apiUrl}/mesocycles/active`)
      .pipe(tap((mesocycle) => this.activeSignal.set(mesocycle)));
  }

  byId(id: string): Observable<MesocycleDto> {
    return this.http.get<MesocycleDto>(`${this.apiUrl}/mesocycles/${id}`);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.apiUrl}/mesocycles/${id}`).pipe(
      tap(() => {
        if (this.activeSignal()?.id === id) {
          this.activeSignal.set(null);
        }
      }),
    );
  }
}
