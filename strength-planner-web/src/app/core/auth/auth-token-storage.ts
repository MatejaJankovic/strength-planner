import { Injectable, signal } from '@angular/core';

export const TOKEN_KEY = 'strength-planner.token';
export const REFRESH_TOKEN_KEY = 'strength-planner.refresh-token';

/**
 * The two tokens of the signed-in account: a short-lived access token sent with every request, and
 * the refresh token it is renewed with.
 *
 * `localStorage` is shared by all tabs, so the values are read from it each time they are used. The
 * signal exists for the templates and guards, and is brought up to date by `sync()` when another tab
 * writes.
 */
@Injectable({ providedIn: 'root' })
export class AuthTokenStorage {
  private readonly tokenSignal = signal<string | null>(localStorage.getItem(TOKEN_KEY));

  readonly token = this.tokenSignal.asReadonly();

  /** Read from storage and not from the signal: another tab may have refreshed a moment ago. */
  accessToken(): string | null {
    return localStorage.getItem(TOKEN_KEY);
  }

  refreshToken(): string | null {
    return localStorage.getItem(REFRESH_TOKEN_KEY);
  }

  save(accessToken: string, refreshToken: string): void {
    // The refresh token goes first: a tab that sees the new access token must find its pair there.
    localStorage.setItem(REFRESH_TOKEN_KEY, refreshToken);
    localStorage.setItem(TOKEN_KEY, accessToken);
    this.tokenSignal.set(accessToken);
  }

  clear(): void {
    localStorage.removeItem(REFRESH_TOKEN_KEY);
    localStorage.removeItem(TOKEN_KEY);
    this.tokenSignal.set(null);
  }

  /** Takes over what another tab wrote. */
  sync(): void {
    this.tokenSignal.set(localStorage.getItem(TOKEN_KEY));
  }
}

/**
 * The account an access token belongs to, from its `sub` claim. Only for telling accounts apart on
 * this device; the server decides what a token is worth.
 */
export function accountOf(token: string | null): string | null {
  const payload = token?.split('.')[1];
  if (!payload) {
    return null;
  }

  try {
    const claims: unknown = JSON.parse(atob(payload.replace(/-/g, '+').replace(/_/g, '/')));
    const subject = (claims as { sub?: unknown }).sub;
    return typeof subject === 'string' ? subject : null;
  } catch {
    return null;
  }
}
