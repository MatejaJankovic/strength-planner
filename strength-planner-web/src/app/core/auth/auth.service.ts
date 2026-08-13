import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from '../api/api-base';
import { ExerciseService } from '../api/exercise.service';
import { MacrocycleService } from '../api/macrocycle.service';
import { MesocycleService } from '../api/mesocycle.service';
import { OneRepMaxService } from '../api/one-rep-max.service';
import { AuthResponseDto, CurrentUserDto, LoginDto, RegisterDto, UpdateProfileDto } from '../models/auth.models';
import { AuthTokenStorage } from './auth-token-storage';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);
  private readonly tokenStorage = inject(AuthTokenStorage);
  private readonly router = inject(Router);
  private readonly exerciseService = inject(ExerciseService);
  private readonly mesocycleService = inject(MesocycleService);
  private readonly macrocycleService = inject(MacrocycleService);
  private readonly oneRepMaxService = inject(OneRepMaxService);

  readonly token = this.tokenStorage.token;

  private readonly currentUserSignal = signal<CurrentUserDto | null>(null);
  readonly currentUser = this.currentUserSignal.asReadonly();

  readonly isAuthenticated = computed(() => this.token() !== null);

  login(dto: LoginDto): Observable<AuthResponseDto> {
    return this.http
      .post<AuthResponseDto>(`${this.apiUrl}/auth/login`, dto)
      .pipe(tap((response) => this.handleAuthenticated(response)));
  }

  register(dto: RegisterDto): Observable<AuthResponseDto> {
    return this.http
      .post<AuthResponseDto>(`${this.apiUrl}/auth/register`, dto)
      .pipe(tap((response) => this.handleAuthenticated(response)));
  }

  loadMe(): Observable<CurrentUserDto> {
    return this.http
      .get<CurrentUserDto>(`${this.apiUrl}/auth/me`)
      .pipe(tap((user) => this.currentUserSignal.set(user)));
  }

  updateProfile(dto: UpdateProfileDto): Observable<CurrentUserDto> {
    return this.http
      .put<CurrentUserDto>(`${this.apiUrl}/auth/profile`, dto)
      .pipe(tap((user) => this.currentUserSignal.set(user)));
  }

  logout(): void {
    this.tokenStorage.clear();
    this.currentUserSignal.set(null);
    this.resetUserCaches();
    void this.router.navigate(['/login']);
  }

  private handleAuthenticated(response: AuthResponseDto): void {
    this.tokenStorage.setToken(response.token);
    this.currentUserSignal.set({ id: response.userId, email: response.email });
    // Novi identitet — keširani podaci prethodnog korisnika ne smeju da procure.
    this.resetUserCaches();
  }

  /**
   * Prazni sve keševe vezane za korisnika.
   *
   * Servisi žive na nivou cele aplikacije, pa promena identiteta bez osvežavanja stranice
   * ostavlja podatke prethodnog korisnika u memoriji. **Svaki servis koji kešira korisničke
   * podatke mora da bude naveden ovde.** `auth.service.spec.ts` proverava ova četiri —
   * peti, dodat kasnije, neće biti pokriven dok se i tamo ne doda.
   */
  private resetUserCaches(): void {
    this.exerciseService.reset();
    this.mesocycleService.reset();
    this.macrocycleService.reset();
    this.oneRepMaxService.reset();
  }
}
