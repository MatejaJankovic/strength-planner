import { HttpClient } from '@angular/common/http';
import { computed, inject, Injectable, signal } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, tap } from 'rxjs';
import { API_BASE_URL } from '../api/api-base';
import { AuthResponseDto, CurrentUserDto, LoginDto, RegisterDto } from '../models/auth.models';
import { AuthTokenStorage } from './auth-token-storage';

@Injectable({ providedIn: 'root' })
export class AuthService {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);
  private readonly tokenStorage = inject(AuthTokenStorage);
  private readonly router = inject(Router);

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

  logout(): void {
    this.tokenStorage.clear();
    this.currentUserSignal.set(null);
    void this.router.navigate(['/login']);
  }

  private handleAuthenticated(response: AuthResponseDto): void {
    this.tokenStorage.setToken(response.token);
    this.currentUserSignal.set({ userId: response.userId, email: response.email });
  }
}
