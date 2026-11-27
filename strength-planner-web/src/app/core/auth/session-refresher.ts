import { HttpClient, HttpErrorResponse } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { catchError, defer, firstValueFrom, from, Observable, throwError, timeout, TimeoutError } from 'rxjs';
import { API_BASE_URL } from '../api/api-base';
import { AuthResponseDto } from '../models/auth.models';
import { AuthTokenStorage } from './auth-token-storage';

/** The sign-in on this device is over: there is nothing left to renew it with. */
export class SessionEndedError extends Error {
  constructor() {
    super('The session has ended.');
  }
}

/** How long a renewal may hang before the requests waiting on it are given up. */
export const REFRESH_TIMEOUT_MS = 15_000;

/**
 * How long to wait for another tab to finish renewing. Longer than its own deadline, but not without
 * one: a tab the phone has put to sleep while it holds the lock would otherwise leave every request
 * of this one waiting, with nothing on the screen to say so.
 */
export const REFRESH_LOCK_WAIT_MS = REFRESH_TIMEOUT_MS + 5_000;

const REFRESH_LOCK = 'strength-planner.session-refresh';

/**
 * Renews the access token with the refresh token, once for however many requests were refused.
 *
 * The server replaces the refresh token on every use and takes a replaced one for theft, so two
 * renewals must never run side by side. Requests of one tab share a single call; tabs queue on a Web
 * Lock, and whoever gets it second finds the new tokens already stored. Web Locks need a secure
 * context, which the deployed app and `localhost` are.
 */
@Injectable({ providedIn: 'root' })
export class SessionRefresher {
  private readonly http = inject(HttpClient);
  private readonly apiUrl = inject(API_BASE_URL);
  private readonly storage = inject(AuthTokenStorage);

  private readonly refreshUrl = `${this.apiUrl}/auth/refresh`;

  private pending: { rejected: string; token: Promise<string> } | null = null;

  /**
   * An access token to use instead of the one the server refused.
   *
   * Fails with `SessionEndedError` when the sign-in is over. Any other failure (no network, a server
   * that is down or busy) says nothing about the sign-in, and is passed on as it is.
   */
  refresh(rejected: string): Observable<string> {
    return defer(() => {
      if (this.pending?.rejected !== rejected) {
        const token = this.exclusively(() => this.exchange(rejected));
        const started = { rejected, token };
        this.pending = started;

        const forget = () => {
          if (this.pending === started) {
            this.pending = null;
          }
        };
        token.then(forget, forget);
      }

      return from(this.pending.token);
    });
  }

  private async exclusively(work: () => Promise<string>): Promise<string> {
    const locks: LockManager | undefined = globalThis.navigator?.locks;
    if (!locks) {
      return work();
    }

    // An AbortController and a timer, not `AbortSignal.timeout`, which older Safari lacks.
    const waiting = new AbortController();
    const deadline = setTimeout(() => waiting.abort(), REFRESH_LOCK_WAIT_MS);

    try {
      return await locks.request(REFRESH_LOCK, { signal: waiting.signal }, () => {
        clearTimeout(deadline);
        return work();
      });
    } catch (error) {
      // The signal can only end the wait, and the timer is stopped once the lock is granted.
      throw waiting.signal.aborted ? this.noAnswer() : error;
    } finally {
      clearTimeout(deadline);
    }
  }

  /** A renewal that got nowhere, reported like any other request without an answer. */
  private noAnswer(): HttpErrorResponse {
    return new HttpErrorResponse({ status: 0, url: this.refreshUrl });
  }

  private async exchange(rejected: string): Promise<string> {
    const current = this.storage.accessToken();
    if (current && current !== rejected) {
      return current;
    }

    const refreshToken = this.storage.refreshToken();
    if (!refreshToken) {
      throw new SessionEndedError();
    }

    try {
      const response = await firstValueFrom(
        this.http.post<AuthResponseDto>(this.refreshUrl, { refreshToken }).pipe(
          timeout(REFRESH_TIMEOUT_MS),
          // So that screens show their usual message.
          catchError((error: unknown) =>
            throwError(() => (error instanceof TimeoutError ? this.noAnswer() : error)),
          ),
        ),
      );

      // Signed out, or signed in anew, while the answer was on its way: it must not undo that.
      if (this.storage.refreshToken() !== refreshToken) {
        return this.storedToken();
      }

      this.storage.save(response.token, response.refreshToken);
      return response.token;
    } catch (error) {
      if (error instanceof HttpErrorResponse && error.status === 401) {
        if (this.storage.refreshToken() !== refreshToken) {
          return this.storedToken();
        }

        throw new SessionEndedError();
      }

      throw error;
    }
  }

  private storedToken(): string {
    const token = this.storage.accessToken();
    if (!token) {
      throw new SessionEndedError();
    }

    return token;
  }
}
