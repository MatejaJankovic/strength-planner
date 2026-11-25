import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from './api-base';
import { NoteDto } from '../models/training.models';

/** Beleške: jedna uz trening i jedna uz vežbu (prikazuje se pri svakom treningu te vežbe). */
@Injectable({ providedIn: 'root' })
export class NoteService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  /** Postavlja belešku uz trening; prazna je briše. Važi u svakom statusu treninga. */
  setSessionNote(sessionId: string, note: string | null): Observable<NoteDto> {
    return this.http.put<NoteDto>(`${this.apiUrl}/sessions/${sessionId}/note`, { note });
  }

  /** Postavlja belešku uz vežbu (podešavanje sprave, napomena); prazna je briše. */
  setExerciseNote(exerciseId: string, note: string | null): Observable<NoteDto> {
    return this.http.put<NoteDto>(`${this.apiUrl}/exercises/${exerciseId}/note`, { note });
  }
}
