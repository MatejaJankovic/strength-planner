import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable, signal } from '@angular/core';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from './api-base';
import {
  CreateMacrocycleBlockDto,
  CreateMacrocycleRequest,
  Goal,
  MacrocycleDto,
} from '../models/training.models';

@Injectable({ providedIn: 'root' })
export class MacrocycleService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);

  private readonly activeSignal = signal<MacrocycleDto | null>(null);
  readonly active = this.activeSignal.asReadonly();

  /** Ucitava aktivan plan; 404 znaci da korisnik jos nema nijedan. */
  loadActive(): Observable<MacrocycleDto> {
    return this.http
      .get<MacrocycleDto>(`${this.apiUrl}/macrocycles/active`)
      .pipe(tap((plan) => this.activeSignal.set(plan)));
  }

  /**
   * Predlog blokova sa smenjujucim ciljevima. Pravilo smenjivanja zivi u domenu i
   * jedinicno je testirano; klijent ga ne ponavlja svojom logikom.
   */
  suggestedBlocks(
    blockCount: number,
    firstGoal: Goal,
    templateKey: string,
  ): Observable<CreateMacrocycleBlockDto[]> {
    const params = new HttpParams()
      .set('blockCount', blockCount)
      .set('firstGoal', firstGoal)
      .set('templateKey', templateKey);

    return this.http.get<CreateMacrocycleBlockDto[]>(
      `${this.apiUrl}/macrocycles/suggested-blocks`,
      { params },
    );
  }

  create(request: CreateMacrocycleRequest): Observable<MacrocycleDto> {
    return this.http
      .post<MacrocycleDto>(`${this.apiUrl}/macrocycles`, request)
      .pipe(tap((plan) => this.activeSignal.set(plan)));
  }

  /**
   * Brise plan sa svim mezociklusima njegovih blokova. Kes se prazni odmah: posle
   * brisanja aktivnog plana vise nema, pa bi zadrzan plan ostao na ekranu.
   */
  delete(macrocycleId: string): Observable<void> {
    return this.http
      .delete<void>(`${this.apiUrl}/macrocycles/${macrocycleId}`)
      .pipe(tap(() => this.activeSignal.set(null)));
  }

  /** Prazni kes — poziva se pri prijavi/odjavi da podaci ne procure izmedju naloga. */
  reset(): void {
    this.activeSignal.set(null);
  }
}
