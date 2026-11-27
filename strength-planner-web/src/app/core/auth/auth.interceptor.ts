import { HttpErrorResponse, HttpInterceptorFn, HttpRequest } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, switchMap, throwError } from 'rxjs';
import { AuthService } from './auth.service';
import { accountOf, AuthTokenStorage } from './auth-token-storage';
import { SessionEndedError, SessionRefresher } from './session-refresher';

/** Calls that prove who is asking with what they carry, so they get no token and no retry. */
const SESSION_ENDPOINTS = ['/auth/login', '/auth/register', '/auth/refresh', '/auth/logout'];

export const authInterceptor: HttpInterceptorFn = (request, next) => {
  // Their 401 is an answer of their own: wrong credentials, or a refresh token that is no good.
  if (SESSION_ENDPOINTS.some((path) => request.url.endsWith(path))) {
    return next(request);
  }

  const storage = inject(AuthTokenStorage);
  const authService = inject(AuthService);
  const refresher = inject(SessionRefresher);

  const sent = storage.accessToken();

  return next(withToken(request, sent)).pipe(
    catchError((error: unknown) => {
      if (!isUnauthorized(error)) {
        return throwError(() => error);
      }

      if (!sent) {
        authService.endSession();
        return throwError(() => error);
      }

      // The access token ran out: renew it and send the request again. The server refused it before
      // doing anything, so sending a write twice cannot apply it twice.
      return refresher.refresh(sent).pipe(
        catchError((failure: unknown) => {
          if (failure instanceof SessionEndedError) {
            authService.endSession();
            return throwError(() => error);
          }

          // No network or a busy server. The sign-in stays; the screen reports the failure.
          return throwError(() => failure);
        }),
        switchMap((token) => {
          // Another account signed in meanwhile: this request is not theirs to send.
          if (accountOf(token) !== accountOf(sent)) {
            return throwError(() => error);
          }

          return next(withToken(request, token)).pipe(
            catchError((repeated: unknown) => {
              if (isUnauthorized(repeated) && storage.accessToken() === token) {
                authService.endSession();
              }

              return throwError(() => repeated);
            }),
          );
        }),
      );
    }),
  );
};

function withToken(request: HttpRequest<unknown>, token: string | null): HttpRequest<unknown> {
  return token ? request.clone({ setHeaders: { Authorization: `Bearer ${token}` } }) : request;
}

function isUnauthorized(error: unknown): boolean {
  return error instanceof HttpErrorResponse && error.status === 401;
}
