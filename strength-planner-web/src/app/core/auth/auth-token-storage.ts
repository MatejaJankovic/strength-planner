import { Injectable, signal } from '@angular/core';

const TOKEN_KEY = 'strength-planner.token';

@Injectable({ providedIn: 'root' })
export class AuthTokenStorage {
  private readonly tokenSignal = signal<string | null>(localStorage.getItem(TOKEN_KEY));

  readonly token = this.tokenSignal.asReadonly();

  setToken(token: string): void {
    localStorage.setItem(TOKEN_KEY, token);
    this.tokenSignal.set(token);
  }

  clear(): void {
    localStorage.removeItem(TOKEN_KEY);
    this.tokenSignal.set(null);
  }
}
